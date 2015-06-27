// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Gateway.Samples.Common
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Net;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Mqtt;
    using DotNetty.Common.Concurrency;
    using DotNetty.Handlers.Tls;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using Microsoft.Azure.Devices.Gateway.Core;
    using Microsoft.Azure.Devices.Gateway.Core.Mqtt;

    public class Bootstrapper
    {
        const int MqttPort = 1883;
        const int MqttsPort = 8883;
        const int ListenBacklogSize = 100; // 100 connections allowed pending accept

        readonly TaskCompletionSource closeCompletionSource;
        readonly ISettingsProvider settingsProvider;
        readonly Settings settings;
        readonly ISessionStateManager sessionStateManager;
        readonly IAuthenticationProvider authProvider;
        X509Certificate2 tlsCertificate;
        IEventLoopGroup eventLoopGroup;
        IByteBufferAllocator bufferAllocator;
        IChannel serverChannel;
        IChannel secureServerChannel;

        public Bootstrapper(ISettingsProvider settingsProvider, ISessionStateManager sessionStateManager)
        {
            Contract.Requires(settingsProvider != null);
            Contract.Requires(sessionStateManager != null);

            this.closeCompletionSource = new TaskCompletionSource();

            this.settingsProvider = settingsProvider;
            this.settings = new Settings(this.settingsProvider);
            this.sessionStateManager = sessionStateManager;
            this.authProvider = new StubAuthenticationProvider();
        }

        public Task CloseCompletion
        {
            get { return this.closeCompletionSource.Task; }
        }

        public async Task RunAsync(X509Certificate2 certificate, int threadCount, CancellationToken cancellationToken)
        {
            try
            {
                Contract.Requires(threadCount > 0);

                BootstrapperEventSource.Log.Info("Starting", null);

                this.tlsCertificate = certificate;
                this.eventLoopGroup = new MultithreadEventLoopGroup(threadCount);
                this.bufferAllocator = new PooledByteBufferAllocator(16 * 1024, 300 * 1024 * 1024 / threadCount); // reserve 300 MB of 16 KB buffers

                ServerBootstrap bootstrap = this.SetupBootstrap();
                this.serverChannel = await bootstrap.BindAsync(IPAddress.Any, MqttPort);
                if (this.tlsCertificate == null)
                {
                    BootstrapperEventSource.Log.Info("No certificate has been provided. Skipping TLS endpoint initialization.", null);
                }
                else
                {
                    BootstrapperEventSource.Log.Info(string.Format("Initializing TLS endpoint with certificate {0}.", this.tlsCertificate.Thumbprint), null);
                    this.secureServerChannel = await bootstrap.BindAsync(IPAddress.Any, MqttsPort);
                }

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
                if (this.secureServerChannel != null)
                {
                    await this.secureServerChannel.CloseAsync();
                }
                await this.eventLoopGroup.ShutdownGracefullyAsync();

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
            int maxInboundMessageSize = this.settingsProvider.GetIntegerSetting("MaxInboundMessageSize");

            return new ServerBootstrap()
                .Group(this.eventLoopGroup)
                .Option(ChannelOption.SoBacklog, ListenBacklogSize)
                .ChildOption(ChannelOption.Allocator, this.bufferAllocator)
                .ChildOption(ChannelOption.AutoRead, false)
                .Channel<TcpServerSocketChannel>()
                .ChildHandler(new ActionChannelInitializer<ISocketChannel>(channel =>
                {
                    if (((IPEndPoint)channel.RemoteAddress).Port == MqttsPort)
                    {
                        channel.Pipeline.AddLast(new TlsHandler(this.tlsCertificate));
                    }
                    channel.Pipeline.AddLast(
                        ServerEncoder.Instance,
                        new ServerDecoder(maxInboundMessageSize),
                        new BridgeDriver(this.settings, this.sessionStateManager, this.authProvider));
                }));
        }
    }
}