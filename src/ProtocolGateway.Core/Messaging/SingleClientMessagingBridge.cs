// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Messaging
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.ProtocolGateway.Identity;
    using Microsoft.Azure.Devices.ProtocolGateway.Instrumentation;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt;

    public sealed class SingleClientMessagingBridge : IMessagingBridge
    {
        readonly IDeviceIdentity deviceIdentity;
        readonly IMessagingServiceClient messagingClient;
        IMessagingChannel messagingChannel;

        public SingleClientMessagingBridge(IDeviceIdentity deviceIdentity, IMessagingServiceClient messagingClient)
        {
            this.deviceIdentity = deviceIdentity;
            this.messagingClient = messagingClient;
        }

        public void BindMessagingChannel(IMessagingChannel channel)
        {
            this.messagingChannel = channel;
            this.messagingClient.BindMessagingChannel(channel);
        }

        public bool TryResolveClient(string topicName, out IMessagingServiceClient sendingClient)
        {
            sendingClient = this.messagingClient;
            return true;
        }

        public Task DisposeAsync(Exception cause)
        {
            if (cause == null)
            {
                CommonEventSource.Log.Info($"Closing connection for device: {this.deviceIdentity}", string.Empty, string.Empty);
            }
            else
            {
                string operationScope = cause.Data[MqttAdapter.OperationScopeExceptionDataKey]?.ToString();
                string connectionScope = cause.Data[MqttAdapter.ConnectionScopeExceptionDataKey]?.ToString();
                CommonEventSource.Log.Warning($"Closing connection for device: {this.deviceIdentity}" + (operationScope == null ? null : ", scope: " + operationScope), cause, connectionScope);
            }

            return this.messagingClient.DisposeAsync(cause);
        }

        public void Close(Exception cause) => this.messagingChannel.Close(cause);
    }
}