// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.IotHubClient
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.ProtocolGateway.IotHub;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Auth;
    using IotHubCommunicationException = Microsoft.Azure.Devices.ProtocolGateway.IotHub.IotHubCommunicationException;

    public class IotHubClient : IIotHubClient
    {
        readonly DeviceClient deviceClient;

        public IotHubClient(DeviceClient deviceClient)
        {
            this.deviceClient = deviceClient;
        }

        public static async Task<IIotHubClient> CreateFromConnectionStringAsync(string connectionString)
        {
            DeviceClient client = DeviceClient.CreateFromConnectionString(connectionString);
            await ExecuteDeviceClientAction(() => client.OpenAsync());
            return new IotHubClient(client);
        }

        public static IotHubClientFactoryFunc PreparePoolFactory(string baseConnectionString, string poolId, int connectionPoolSize)
        {
            IotHubConnectionStringBuilder csb = IotHubConnectionStringBuilder.Create(baseConnectionString);
            // todo: uncommment once explicit control over connection pooling is available
            //string[] connectionIds = Enumerable.Range(1, connectionPoolSize).Select(index => poolId + index).ToArray();
            int connectionIndex = 0;
            IotHubClientFactoryFunc iotHubClientFactory = deviceCredentials =>
            {
                if (++connectionIndex >= connectionPoolSize)
                {
                    connectionIndex = 0;
                }
                //csb.GroupName = connectionIds[connectionIndex]; // todo: uncommment once explicit control over connection pooling is available
                var identity = (IotHubIdentity)deviceCredentials.Identity;
                csb.AuthenticationMethod = DeriveAuthenticationMethod(csb.AuthenticationMethod, identity.DeviceId, deviceCredentials.Properties);
                csb.HostName = identity.IotHubHostName;
                string connectionString = csb.ToString();
                return CreateFromConnectionStringAsync(connectionString);
            };
            return iotHubClientFactory;
        }

        public Task SendAsync(IMessage message)
        {
            return ExecuteDeviceClientAction(() => this.deviceClient.SendEventAsync(((IotHubMessage)message).ToMessage()));
        }

        public async Task<IMessage> ReceiveAsync()
        {
            return await ExecuteDeviceClientAction(async () =>
            {
                Message message = await this.deviceClient.ReceiveAsync(TimeSpan.MaxValue);
                return new IotHubMessage(message);
            });
        }

        public Task AbandonAsync(string lockToken)
        {
            return ExecuteDeviceClientAction(() => this.deviceClient.AbandonAsync(lockToken));
        }

        public Task CompleteAsync(string lockToken)
        {
            return ExecuteDeviceClientAction(() => this.deviceClient.CompleteAsync(lockToken));
        }

        public Task RejectAsync(string lockToken)
        {
            return ExecuteDeviceClientAction(() => this.deviceClient.RejectAsync(lockToken));
        }

        public Task DisposeAsync()
        {
            return ExecuteDeviceClientAction(() => this.deviceClient.CloseAsync());
        }

        internal static IAuthenticationMethod DeriveAuthenticationMethod(IAuthenticationMethod currentAuthenticationMethod, string deviceId, AuthenticationProperties authenticationProperties)
        {
            switch (authenticationProperties.Scope)
            {
                case AuthenticationScope.None:
                    var policyKeyAuth = currentAuthenticationMethod as DeviceAuthenticationWithSharedAccessPolicyKey;
                    if (policyKeyAuth != null)
                    {
                        return new DeviceAuthenticationWithSharedAccessPolicyKey(deviceId, policyKeyAuth.PolicyName, policyKeyAuth.Key);
                    }
                    var deviceKeyAuth = currentAuthenticationMethod as DeviceAuthenticationWithRegistrySymmetricKey;
                    if (deviceKeyAuth != null)
                    {
                        return new DeviceAuthenticationWithRegistrySymmetricKey(deviceId, deviceKeyAuth.DeviceId);
                    }
                    var deviceTokenAuth = currentAuthenticationMethod as DeviceAuthenticationWithToken;
                    if (deviceTokenAuth != null)
                    {
                        return new DeviceAuthenticationWithToken(deviceId, deviceTokenAuth.Token);
                    }
                    throw new InvalidOperationException("");
                case AuthenticationScope.SasToken:
                    return new DeviceAuthenticationWithToken(deviceId, authenticationProperties.Secret);
                case AuthenticationScope.DeviceKey:
                    return new DeviceAuthenticationWithRegistrySymmetricKey(deviceId, authenticationProperties.Secret);
                case AuthenticationScope.HubKey:
                    return new DeviceAuthenticationWithSharedAccessPolicyKey(deviceId, authenticationProperties.PolicyName, authenticationProperties.Secret);
                default:
                    throw new InvalidOperationException("Unexpected AuthenticationScope value: " + authenticationProperties.Scope);
            }
        }

        static async Task<T> ExecuteDeviceClientAction<T>(Func<Task<T>> action)
        {
            try
            {
                return await action();
            }
            catch (IotHubException ex)
            {
                throw new IotHubCommunicationException(ex.Message, ex.InnerException, ex.IsTransient, ex.TrackingId);
            }
        }

        static Task ExecuteDeviceClientAction(Func<Task> action)
        {
            return ExecuteDeviceClientAction(async () => { await action(); return 0; });
        }
    }
}