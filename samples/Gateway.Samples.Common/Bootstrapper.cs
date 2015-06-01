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
    using DotNetty.Transport;
    using Microsoft.Azure.Devices.Gateway.Cloud;
    using Microsoft.Azure.Devices.Gateway.Core;
    using Microsoft.Azure.Devices.Gateway.Core.Mqtt;

    public class Bootstrapper
    {
        const int MqttPort = 1883;
        const int MqttsPort = 8883;
        const int ListenBacklogSize = 100; // 100 connections allowed pending accept

        readonly TaskCompletionSource<int> completionTcs;
        readonly ISettingsProvider settingsProvider;
        readonly Settings settings;
        readonly ISessionStateManager sessionStateManager;
        readonly IAuthenticationProvider authProvider;
        X509Certificate2 tlsCertificate;
        IExecutorGroup executorGroup;
        IByteBufferAllocator bufferAllocator;
        ServerBootstrap server;
        ServerBootstrap secureServer;

        public Bootstrapper(ISettingsProvider settingsProvider, BlobSessionStateManager sessionStateManager)
        {
            Contract.Requires(settingsProvider != null);
            Contract.Requires(sessionStateManager != null);

            this.completionTcs = new TaskCompletionSource<int>();

            this.settingsProvider = settingsProvider;
            this.settings = new Settings(this.settingsProvider);
            this.sessionStateManager = sessionStateManager;
            this.authProvider = new StubAuthenticationProvider();
        }

        public Task CloseCompletion
        {
            get { return this.completionTcs.Task; }
        }

        public Task RunAsync(X509Certificate2 certificate, int threadCount, CancellationToken cancellationToken)
        {
            try
            {
                Contract.Requires(threadCount > 0);

                BootstrapperEventSource.Log.Info("Starting", null);

                this.tlsCertificate = certificate;
                this.executorGroup = new FixedThreadSetExecutorGroup(name => new MpscQueueThreadExecutor(name, TimeSpan.FromSeconds(5)), threadCount, "dotnetty-exec");
                this.bufferAllocator = new ThreadLocalFixedPooledHeapByteBufferAllocator(16 * 1024, 300 * 1024 * 1024 / threadCount); // reserve 300 MB of 16 KB buffers

                this.server = this.SetupServer(threadCount, false);
                this.server.Start(new IPEndPoint(IPAddress.Any, MqttPort), ListenBacklogSize);
                if (this.tlsCertificate == null)
                {
                    BootstrapperEventSource.Log.Info("No certificate has been provided. Skipping TLS endpoint initialization.", null);
                }
                else
                {
                    BootstrapperEventSource.Log.Info(string.Format("Initializing TLS endpoint with certificate {0}.", this.tlsCertificate.Thumbprint), null);
                    this.secureServer = this.SetupServer(threadCount, true);
                    this.secureServer.Start(new IPEndPoint(IPAddress.Any, MqttsPort), ListenBacklogSize);
                }

                cancellationToken.Register(this.CloseAsync);

                BootstrapperEventSource.Log.Info("Started", null);
            }
            catch (Exception ex)
            {
                BootstrapperEventSource.Log.Error("Failed to start", ex);
                this.CloseAsync();
            }
            return this.CloseCompletion;
        }

        async void CloseAsync()
        {
            try
            {
                BootstrapperEventSource.Log.Info("Stopping", null);

                // todo: change once shutdown is supported in IEventExecutorGroup and server channel support is in
                for (IExecutor currentExecutor = this.executorGroup.GetNext(); currentExecutor.Running; currentExecutor = this.executorGroup.GetNext())
                {
                    currentExecutor.Running = false;
                }

                this.server.Dispose();
                this.secureServer.Dispose();

                await Task.WhenAll(this.server.Completion, this.secureServer.Completion);

                BootstrapperEventSource.Log.Info("Stopped", null);
            }
            catch (Exception ex)
            {
                BootstrapperEventSource.Log.Warning("Failed to stop cleanly", ex);
            }
            finally
            {
                this.completionTcs.TrySetResult(0);
            }
        }

        ServerBootstrap SetupServer(int threadCount, bool secure)
        {
            int maxInboundMessageSize = this.settingsProvider.GetIntegerSetting("MaxInboundMessageSize");

            return new ServerBootstrap()
                .ExecutorGroup(this.executorGroup)
                .Allocator(this.bufferAllocator)
                .DataChannelInitialization(channel =>
                {
                    if (secure)
                    {
                        channel.Pipeline.AddLast(new TlsHandler(this.tlsCertificate));
                    }
                    channel.Pipeline.AddLast(
                        ServerEncoder.Instance,
                        new ServerDecoder(maxInboundMessageSize),
                        new BridgeDriver(this.settings, this.sessionStateManager, this.authProvider));
                })
                .DataChannelConfiguration(config =>
                {
                    config.AutoRead = false;
                    config.WriteBufferHighWaterMark = 64 * 1024;
                    config.ReceiveBufferSize = 16 * 1024;
                    config.SendBufferSize = 16 * 1024;
                    config.ReceiveEventPoolSize = 200 * 1000 / threadCount; // do not expect to have more than 200K connections (though it will still scale beyond that if necessary
                    config.SendEventPoolSize = 200 * 1000 / threadCount;
                });
        }
    }
}