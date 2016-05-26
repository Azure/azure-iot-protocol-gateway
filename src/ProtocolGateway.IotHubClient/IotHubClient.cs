// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.IotHubClient
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.ProtocolGateway.Messaging;

    public class IotHubClient : IMessagingServiceClient
    {
        readonly DeviceClient deviceClient;

        public IotHubClient(DeviceClient deviceClient)
        {
            this.deviceClient = deviceClient;
        }

        public static async Task<IMessagingServiceClient> CreateFromConnectionStringAsync(string connectionString, int connectionPoolSize, TimeSpan? connectionIdleTimeout)
        {
            DeviceClient client;
            if (connectionPoolSize > 0)
            {
                var amqpConnectionPoolSettings = new AmqpConnectionPoolSettings
                {
                    MaxPoolSize = unchecked ((uint)connectionPoolSize),
                    Pooling = connectionPoolSize > 0
                };
                if (connectionIdleTimeout.HasValue)
                {
                    amqpConnectionPoolSettings.ConnectionIdleTimeout = connectionIdleTimeout.Value;
                }
                var transportSettings = new ITransportSettings[]
                {
                    new AmqpTransportSettings(TransportType.Amqp_Tcp_Only)
                    {
                        AmqpConnectionPoolSettings = amqpConnectionPoolSettings
                    },
                    new AmqpTransportSettings(TransportType.Amqp_WebSocket_Only)
                    {
                        AmqpConnectionPoolSettings = amqpConnectionPoolSettings
                    }
                };
                client = DeviceClient.CreateFromConnectionString(connectionString, transportSettings);
            }
            else
            {
                client = DeviceClient.CreateFromConnectionString(connectionString);
            }
            try
            {
                await client.OpenAsync();
            }
            catch (IotHubException ex)
            {
                throw ComposeIotHubCommunicationException(ex);
            }
            return new IotHubClient(client);
        }

        public static IotHubClientFactoryFunc PreparePoolFactory(string baseConnectionString, string poolId)
        {
            return PreparePoolFactory(baseConnectionString, poolId, 0, null);
        }

        public static IotHubClientFactoryFunc PreparePoolFactory(string baseConnectionString, string poolId, int connectionPoolSize, TimeSpan? connectionIdleTimeout)
        {
            IotHubConnectionStringBuilder csb = IotHubConnectionStringBuilder.Create(baseConnectionString);
            IotHubClientFactoryFunc iotHubClientFactory = deviceIdentity =>
            {
                var identity = (IotHubDeviceIdentity)deviceIdentity;
                csb.AuthenticationMethod = DeriveAuthenticationMethod(csb.AuthenticationMethod, identity);
                csb.HostName = identity.IotHubHostName;
                string connectionString = csb.ToString();
                return CreateFromConnectionStringAsync(connectionString, connectionPoolSize, connectionIdleTimeout);
            };
            return iotHubClientFactory;
        }

        public async Task SendAsync(IMessage message)
        {
            try
            {
                await this.deviceClient.SendEventAsync(((DeviceClientMessage)message).ToMessage());
            }
            catch (IotHubException ex)
            {
                throw ComposeIotHubCommunicationException(ex);
            }
        }

        public async Task<IMessage> ReceiveAsync()
        {
            try
            {
                Message message = await this.deviceClient.ReceiveAsync(TimeSpan.MaxValue);
                return message == null ? null : new DeviceClientMessage(message);
            }
            catch (IotHubException ex)
            {
                throw new MessagingException(ex.Message, ex.InnerException, ex.IsTransient, ex.TrackingId);
            }
        }

        public async Task AbandonAsync(string lockToken)
        {
            try
            {
                await this.deviceClient.AbandonAsync(lockToken);
            }
            catch (IotHubException ex)
            {
                throw ComposeIotHubCommunicationException(ex);
            }
        }

        public async Task CompleteAsync(string lockToken)
        {
            try
            {
                await this.deviceClient.CompleteAsync(lockToken);
            }
            catch (IotHubException ex)
            {
                throw ComposeIotHubCommunicationException(ex);
            }
        }

        public async Task RejectAsync(string lockToken)
        {
            try
            {
                await this.deviceClient.RejectAsync(lockToken);
            }
            catch (IotHubException ex)
            {
                throw ComposeIotHubCommunicationException(ex);
            }
        }

        public async Task DisposeAsync()
        {
            try
            {
                await this.deviceClient.CloseAsync();
            }
            catch (IotHubException ex)
            {
                throw ComposeIotHubCommunicationException(ex);
            }
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
    }
}