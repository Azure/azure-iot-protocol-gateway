// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ProtocolGateway.Host.Common
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Net;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using DotNetty.Codecs.Mqtt;
    using DotNetty.Common.Concurrency;
    using DotNetty.Handlers.Tls;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.ProtocolGateway;
    using Microsoft.Azure.Devices.ProtocolGateway.Identity;
    using Microsoft.Azure.Devices.ProtocolGateway.Instrumentation;
    using Microsoft.Azure.Devices.ProtocolGateway.IotHubClient;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence;
    using Message = Microsoft.Azure.Devices.ProtocolGateway.Messaging.Message;

    public class Bootstrapper
    {
        const int MqttsPort = 8883;
        const int ListenBacklogSize = 2000; // connections allowed pending accept
        const int MaxConcurrentAccepts = 200; // Maximum number of concurrent connections to accept and start TLS handshake with
        const int DefaultConnectionPoolSize = 400; // IoT Hub default connection pool size
        static readonly TimeSpan DefaultConnectionIdleTimeout = TimeSpan.FromSeconds(210); // IoT Hub default connection idle timeout

        readonly TaskCompletionSource closeCompletionSource;
        readonly ISettingsProvider settingsProvider;
        readonly Settings settings;
        readonly ISessionStatePersistenceProvider sessionStateManager;
        readonly IQos2StatePersistenceProvider qos2StateProvider;
        readonly IDeviceIdentityProvider authProvider;
        X509Certificate2 tlsCertificate;
        IEventLoopGroup parentEventLoopGroup;
        IEventLoopGroup eventLoopGroup;
        IChannel serverChannel;
        readonly IotHubClientSettings iotHubClientSettings;

        public Bootstrapper(ISettingsProvider settingsProvider, ISessionStatePersistenceProvider sessionStateManager, IQos2StatePersistenceProvider qos2StateProvider)
        {
            Contract.Requires(settingsProvider != null);
            Contract.Requires(sessionStateManager != null);

            this.closeCompletionSource = new TaskCompletionSource();

            this.settingsProvider = settingsProvider;
            this.settings = new Settings(this.settingsProvider);
            this.iotHubClientSettings = new IotHubClientSettings(this.settingsProvider);
            this.sessionStateManager = sessionStateManager;
            this.qos2StateProvider = qos2StateProvider;
            this.authProvider = new SasTokenDeviceIdentityProvider();
        }

        public Task CloseCompletion => this.closeCompletionSource.Task;

        public async Task RunAsync(X509Certificate2 certificate, int threadCount, CancellationToken cancellationToken)
        {
            Contract.Requires(certificate != null);
            Contract.Requires(threadCount > 0);

            try
            {
                BootstrapperEventSource.Log.Info("Starting", null);

                PerformanceCounters.ConnectionsEstablishedTotal.RawValue = 0;
                PerformanceCounters.ConnectionsCurrent.RawValue = 0;
                PerformanceCounters.TotalCommandsReceived.RawValue = 0;
                PerformanceCounters.TotalMethodsInvoked.RawValue = 0;

                this.tlsCertificate = certificate;
                this.parentEventLoopGroup = new MultithreadEventLoopGroup(1);
                this.eventLoopGroup = new MultithreadEventLoopGroup(threadCount);

                ServerBootstrap bootstrap = this.SetupBootstrap();
                BootstrapperEventSource.Log.Info($"Initializing TLS endpoint on port {MqttsPort.ToString()} with certificate {this.tlsCertificate.Thumbprint}.", null);
                this.serverChannel = await bootstrap.BindAsync(IPAddress.Any, MqttsPort);

                this.serverChannel.CloseCompletion.LinkOutcome(this.closeCompletionSource);
                cancellationToken.Register(this.CloseAsync);

                BootstrapperEventSource.Log.Info("Started", null);
            }
            catch (Exception ex)
            {
                BootstrapperEventSource.Log.Error("Failed to start", ex);
                this.CloseAsync();
            }
        }

        async void CloseAsync()
        {
            try
            {
                BootstrapperEventSource.Log.Info("Stopping", null);

                if (this.serverChannel != null)
                {
                    await this.serverChannel.CloseAsync();
                }
                if (this.eventLoopGroup != null)
                {
                    await this.eventLoopGroup.ShutdownGracefullyAsync();
                }

                BootstrapperEventSource.Log.Info("Stopped", null);
            }
            catch (Exception ex)
            {
                BootstrapperEventSource.Log.Warning("Failed to stop cleanly", ex);
            }
            finally
            {
                this.closeCompletionSource.TryComplete();
            }
        }

        ServerBootstrap SetupBootstrap()
        {
            // pull/customize configuration
            int maxInboundMessageSize = this.settingsProvider.GetIntegerSetting("MaxInboundMessageSize", 256 * 1024);
            int connectionPoolSize = this.settingsProvider.GetIntegerSetting("IotHubClient.ConnectionPoolSize", DefaultConnectionPoolSize);
            TimeSpan connectionIdleTimeout = this.settingsProvider.GetTimeSpanSetting("IotHubClient.ConnectionIdleTimeout", DefaultConnectionIdleTimeout);
            string connectionString = this.iotHubClientSettings.IotHubConnectionString;

            // setup message processing logic
            var telemetryProcessing = TopicHandling.CompileParserFromUriTemplates(new[] { "devices/{deviceId}/messages/events" });
            var commandProcessing = TopicHandling.CompileFormatterFromUriTemplate("devices/{deviceId}/messages/devicebound");
            MessagingBridgeFactoryFunc bridgeFactory = IotHubBridge.PrepareFactory(connectionString, connectionPoolSize,
                connectionIdleTimeout, this.iotHubClientSettings, bridge =>
                {
                    bridge.RegisterRoute(topic => true, new TelemetrySender(bridge, telemetryProcessing)); // handle all incoming messages with TelemetrySender
                    bridge.RegisterSource(new CommandReceiver(bridge, PooledByteBufferAllocator.Default, commandProcessing)); // handle device command queue
                    bridge.RegisterSource(new MethodHandler("SendMessageToDevice", bridge, (request, dispatcher) => DispatchCommands(bridge.DeviceId, request, dispatcher))); // register
                });

            var acceptLimiter = new AcceptLimiter(MaxConcurrentAccepts);

            return new ServerBootstrap()
                .Group(this.parentEventLoopGroup, this.eventLoopGroup)
                .Option(ChannelOption.SoBacklog, ListenBacklogSize)
                .Option(ChannelOption.AutoRead, false)
                .ChildOption(ChannelOption.Allocator, UnpooledByteBufferAllocator.Default)
                .ChildOption(ChannelOption.AutoRead, false)
                .Channel<TcpServerSocketChannel>()
                .Handler(acceptLimiter)
                .ChildHandler(new ActionChannelInitializer<ISocketChannel>(channel =>
                {
                    channel.Pipeline.AddLast(
                        TlsHandler.Server(this.tlsCertificate),
                        new AcceptLimiterTlsReleaseHandler(acceptLimiter),
                        MqttEncoder.Instance,
                        new MqttDecoder(true, maxInboundMessageSize),
                        new MqttAdapter(
                            this.settings,
                            this.sessionStateManager,
                            this.authProvider,
                            this.qos2StateProvider,
                            bridgeFactory));
                }));
        }

        static async Task<MethodResponse> DispatchCommands(string deviceId, MethodRequest request, IMessageDispatcher dispatcher)
        {
            PerformanceCounters.TotalMethodsInvoked.Increment();
            PerformanceCounters.MethodsInvokedPerSecond.Increment();

            try
            {
                // deserialize request payload and further process it before sending or
                // just pass it through to message.Payload using Unpooled.WrappedBuffer(request.Data)
                var message = new Message
                {
                    Id = Guid.NewGuid().ToString(),
                    CreatedTimeUtc = DateTime.UtcNow,
                    Address = "devices/" + deviceId + "/messages/commands",
                    Properties = { { "extra", "property" } },
                    Payload = Unpooled.WrappedBuffer(request.Data)
                };

                var outcome = await dispatcher.SendAsync(message);
                switch (outcome)
                {
                    case SendMessageOutcome.Completed:
                        return new MethodResponse(200);
                    case SendMessageOutcome.Rejected:
                        return new MethodResponse(Encoding.UTF8.GetBytes("{\"message\":\"Could not dispatch the call. Device is not subscribed.\"}"), 404);
                    default:
                        return new MethodResponse(Encoding.UTF8.GetBytes("{\"message\":\"Unexpected outcome.\"}"), 500);
                }
            }
            catch (Exception ex)
            {
                CommonEventSource.Log.Error("Received malformed method request: " + request.DataAsJson, ex, null, deviceId);
                return new MethodResponse(Encoding.UTF8.GetBytes($"{{\"message\":\"error sending message: {ex.ToString()}\"}}"), 500);
            }
        }
    }
}