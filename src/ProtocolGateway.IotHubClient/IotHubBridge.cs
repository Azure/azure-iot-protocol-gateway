// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.IotHubClient
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.ProtocolGateway.Instrumentation;
    using Microsoft.Azure.Devices.ProtocolGateway.Messaging;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt;

    public class IotHubBridge : IMessagingBridge
    {
        Exception closedCause;
        IMessagingChannel messagingChannel;
        readonly List<Tuple<Func<string, bool>, IMessagingServiceClient>> routes;
        readonly List<IMessagingSource> sources;

        IotHubBridge(DeviceClient deviceClient, string deviceId, IotHubClientSettings settings)
        {
            this.routes = new List<Tuple<Func<string, bool>, IMessagingServiceClient>>();
            this.sources = new List<IMessagingSource>();
            this.DeviceClient = deviceClient;
            this.DeviceId = deviceId;
            this.Settings = settings;
        }

        public void RegisterRoute(Func<string, bool> routeTopicPredicate, IMessagingServiceClient handler)
        {
            this.routes.Add(Tuple.Create(routeTopicPredicate, handler));
        }

        public void RegisterSource(IMessagingSource source)
        {
            {
                this.sources.Add(source);
                if (this.messagingChannel != null)
                {
                    source.BindMessagingChannel(this.messagingChannel);
                }
            }
        }

        public static MessagingBridgeFactoryFunc PrepareFactory(string baseConnectionString, int connectionPoolSize,
            TimeSpan? connectionIdleTimeout, IotHubClientSettings settings, Action<IotHubBridge> initHandler)
        {
            MessagingBridgeFactoryFunc mqttCommunicatorFactory = async (deviceIdentity, cancellationToken) =>
            {
                var csb = IotHubConnectionStringBuilder.Create(baseConnectionString);
                var identity = (IotHubDeviceIdentity)deviceIdentity;
                csb.AuthenticationMethod = DeriveAuthenticationMethod(csb.AuthenticationMethod, identity);
                csb.HostName = identity.IotHubHostName;
                string connectionString = csb.ToString();
                var bridge = await CreateFromConnectionStringAsync(identity.Id, connectionString, connectionPoolSize, connectionIdleTimeout, settings, cancellationToken);
                initHandler(bridge);
                return bridge;
            };
            return mqttCommunicatorFactory;
        }

        static async Task<IotHubBridge> CreateFromConnectionStringAsync(string deviceId, string connectionString,
            int connectionPoolSize, TimeSpan? connectionIdleTimeout, IotHubClientSettings settings, CancellationToken cancellationToken)
        {
            int maxPendingOutboundMessages = settings.MaxPendingOutboundMessages;
            var tcpSettings = new AmqpTransportSettings(TransportType.Amqp_Tcp_Only);
            var webSocketSettings = new AmqpTransportSettings(TransportType.Amqp_WebSocket_Only);
            webSocketSettings.PrefetchCount = tcpSettings.PrefetchCount = (uint)maxPendingOutboundMessages;
            if (connectionPoolSize > 0)
            {
                var amqpConnectionPoolSettings = new AmqpConnectionPoolSettings
                {
                    MaxPoolSize = unchecked((uint)connectionPoolSize),
                    Pooling = connectionPoolSize > 0
                };
                if (connectionIdleTimeout.HasValue)
                {
                    amqpConnectionPoolSettings.ConnectionIdleTimeout = connectionIdleTimeout.Value;
                }
                tcpSettings.AmqpConnectionPoolSettings = amqpConnectionPoolSettings;
                webSocketSettings.AmqpConnectionPoolSettings = amqpConnectionPoolSettings;
            }
            var client = DeviceClient.CreateFromConnectionString(connectionString, new ITransportSettings[]
            {
                tcpSettings,
                webSocketSettings
            });

            client.SetRetryPolicy(DeviceClientRetryPolicy.Instance);
            var bridge = new IotHubBridge(client, deviceId, settings);
            client.SetConnectionStatusChangesHandler((status, reason) => {
                if (status != ConnectionStatus.Connected)
                {
                    var cause = new IotHubCommunicationException("Connection to IoT Hub is closed");
                    if (Interlocked.CompareExchange(ref bridge.closedCause, cause, null) == null)
                    {
                        bridge.messagingChannel?.Close(cause);
                    }
                }
            });

            // This helps in usage instrumentation at IotHub service.
            client.ProductInfo = $"protocolgateway/poolsize={connectionPoolSize}";

            try
            {
                await client.OpenAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested(); // in case SDK does not always honor cancellation token in async operations
            }
            catch (IotHubException ex)
            {
                var forked = Task.Run(() => client.Dispose());
                throw ex.ToMessagingException();
            }
            catch (Exception)
            {
                var forked = Task.Run(() => client.Dispose());
                throw;
            }
            return bridge;
        }

        public string DeviceId { get; }

        public DeviceClient DeviceClient { get; }

        public IotHubClientSettings Settings { get; }

        public async Task DisposeAsync(Exception cause)
        {
            CommonEventSource.Log.Info("Shutting down: " + cause?.ToString(), null, this.DeviceId);
            try
            {
                await Task.WhenAll(this.routes.Select(r => r.Item2.DisposeAsync(cause)));
            }
            finally
            {
                var forked = Task.Run(async () => {
                    using (this.DeviceClient)
                    {
                        await this.DeviceClient.CloseAsync();
                    }
                })
                .ContinueWith(
                    t => CommonEventSource.Log.Info("Failed to shutdown cleanly: " + t.Exception?.ToString(), null, this.DeviceId),
                    TaskScheduler.Default);
            }
        }

        static IAuthenticationMethod DeriveAuthenticationMethod(IAuthenticationMethod currentAuthenticationMethod, IotHubDeviceIdentity deviceIdentity)
        {
            switch (deviceIdentity.Scope)
            {
                case AuthenticationScope.None:
                    var policyKeyAuth = currentAuthenticationMethod as DeviceAuthenticationWithSharedAccessPolicyKey;
                    if (policyKeyAuth != null)
                    {
                        return new DeviceAuthenticationWithSharedAccessPolicyKey(deviceIdentity.Id, policyKeyAuth.PolicyName, policyKeyAuth.Key);
                    }
                    var deviceKeyAuth = currentAuthenticationMethod as DeviceAuthenticationWithRegistrySymmetricKey;
                    if (deviceKeyAuth != null)
                    {
                        return new DeviceAuthenticationWithRegistrySymmetricKey(deviceIdentity.Id, deviceKeyAuth.DeviceId);
                    }
                    var deviceTokenAuth = currentAuthenticationMethod as DeviceAuthenticationWithToken;
                    if (deviceTokenAuth != null)
                    {
                        return new DeviceAuthenticationWithToken(deviceIdentity.Id, deviceTokenAuth.Token);
                    }
                    throw new InvalidOperationException("");
                case AuthenticationScope.SasToken:
                    return new DeviceAuthenticationWithToken(deviceIdentity.Id, deviceIdentity.Secret);
                case AuthenticationScope.DeviceKey:
                    return new DeviceAuthenticationWithRegistrySymmetricKey(deviceIdentity.Id, deviceIdentity.Secret);
                case AuthenticationScope.HubKey:
                    return new DeviceAuthenticationWithSharedAccessPolicyKey(deviceIdentity.Id, deviceIdentity.PolicyName, deviceIdentity.Secret);
                default:
                    throw new InvalidOperationException("Unexpected AuthenticationScope value: " + deviceIdentity.Scope);
            }
        }

        public void BindMessagingChannel(IMessagingChannel channel)
        {
            Contract.Requires(channel != null);
            Contract.Assert(this.messagingChannel == null);

            this.messagingChannel = channel;
            var closedCause = Volatile.Read(ref this.closedCause);
            if (closedCause != null)
            {
                channel.Close(closedCause);
            }
            foreach (var source in this.sources)
            {
                source.BindMessagingChannel(channel);
            }
        }

        public bool TryResolveClient(string topicName, out IMessagingServiceClient sendingClient)
        {
            foreach (var route in routes)
            {
                if (route.Item1(topicName))
                {
                    sendingClient = route.Item2;
                    return true;
                }
            }

            sendingClient = null;
            return false;
        }
    }
}