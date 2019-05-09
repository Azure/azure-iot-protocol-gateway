// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.IotHubClient
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.IO;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.ProtocolGateway.Identity;
    using Microsoft.Azure.Devices.ProtocolGateway.Instrumentation;
    using Microsoft.Azure.Devices.ProtocolGateway.IotHubClient.Addressing;
    using Microsoft.Azure.Devices.ProtocolGateway.Messaging;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt;

    public class IotHubBridge : IMessagingBridge
    {
        readonly IByteBufferAllocator allocator;
        IMessagingChannel messagingChannel;
        List<Tuple<Func<string, bool>, IMessagingServiceClient>> routes;

        IotHubBridge(DeviceClient deviceClient, string deviceId, IotHubClientSettings settings)
        {
            this.DeviceClient = deviceClient;
            this.DeviceId = deviceId;
            this.Settings = settings;
        }

        public void RegisterRoute(Func<string, bool> topicEvaluator, IMessagingServiceClient handler)
        {
            this.routes.Add(Tuple.Create(topicEvaluator, handler));
            if (this.messagingChannel != null) {
                handler.BindMessagingChannel(this.messagingChannel);
            }
        }

        public static MessagingBridgeFactoryFunc PrepareFactory(string baseConnectionString, int connectionPoolSize,
            TimeSpan? connectionIdleTimeout, IotHubClientSettings settings, Action<IotHubBridge> initHandler)
        {
            MessagingBridgeFactoryFunc mqttCommunicatorFactory = async deviceIdentity =>
            {
                IotHubConnectionStringBuilder csb = IotHubConnectionStringBuilder.Create(baseConnectionString);
                var identity = (IotHubDeviceIdentity)deviceIdentity;
                csb.AuthenticationMethod = DeriveAuthenticationMethod(csb.AuthenticationMethod, identity);
                csb.HostName = identity.IotHubHostName;
                string connectionString = csb.ToString();
                var bridge = await CreateFromConnectionStringAsync(identity.Id, connectionString, connectionPoolSize, connectionIdleTimeout, settings);
                initHandler(bridge);
                return bridge;
            };
            return mqttCommunicatorFactory;
        }

        static async Task<IotHubBridge> CreateFromConnectionStringAsync(string deviceId, string connectionString,
            int connectionPoolSize, TimeSpan? connectionIdleTimeout, IotHubClientSettings settings)
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
            DeviceClient client = DeviceClient.CreateFromConnectionString(connectionString, new ITransportSettings[]
            {
                tcpSettings,
                webSocketSettings
            });

            // This helps in usage instrumentation at IotHub service.
            client.ProductInfo = $"protocolgateway/poolsize={connectionPoolSize}";

            try
            {
                await client.OpenAsync();
            }
            catch (IotHubException ex)
            {
                throw ComposeIotHubCommunicationException(ex);
            }
            return new IotHubBridge(client, deviceId, settings);
        }

        public string DeviceId { get; }
        
        public DeviceClient DeviceClient { get; }

        public IotHubClientSettings Settings { get; }

        public Task DisposeAsync(Exception cause)
        {
            CommonEventSource.Log.Info("Shutting down", cause?.ToString(), this.DeviceId);
            // dispatch to MSCs
            this.DeviceClient.Dispose();
            return TaskEx.Completed;
        }

        internal static IAuthenticationMethod DeriveAuthenticationMethod(IAuthenticationMethod currentAuthenticationMethod, IotHubDeviceIdentity deviceIdentity)
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

        static MessagingException ComposeIotHubCommunicationException(IotHubException ex)
        {
            return new MessagingException(ex.Message, ex.InnerException, ex.IsTransient, ex.TrackingId);
        }

        public void BindMessagingChannel(IMessagingChannel channel)
        {
            Contract.Requires(channel != null);
            Contract.Assert(this.messagingChannel == null);

            this.messagingChannel = channel;
            foreach (var route in this.routes) {
                route.Item2.BindMessagingChannel(channel);
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