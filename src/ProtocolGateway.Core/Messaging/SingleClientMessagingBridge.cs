// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Messaging
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.ProtocolGateway.Identity;
    using Microsoft.Azure.Devices.ProtocolGateway.Instrumentation;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt;

    public sealed class SingleClientMessagingBridge : IMessagingBridge, IMessagingChannel<IMessage>
    {
        readonly IDeviceIdentity deviceIdentity;
        readonly IMessagingServiceClient messagingClient;
        IMessagingChannel<MessageWithFeedback> messagingChannel;

        public SingleClientMessagingBridge(IDeviceIdentity deviceIdentity, IMessagingServiceClient messagingClient)
        {
            this.deviceIdentity = deviceIdentity;
            this.messagingClient = messagingClient;
        }

        public void BindMessagingChannel(IMessagingChannel<MessageWithFeedback> channel)
        {
            this.messagingChannel = channel;
            this.messagingChannel.CapabilitiesChanged += this.OnChannelCapabilitiesChanged;
            this.messagingClient.BindMessagingChannel(this);
        }

        void OnChannelCapabilitiesChanged(object sender, EventArgs args)
        {
            this.CapabilitiesChanged?.Invoke(this, EventArgs.Empty);
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

            if (this.messagingChannel != null)
            {
                this.messagingChannel.CapabilitiesChanged -= this.OnChannelCapabilitiesChanged;
            }
            return this.messagingClient.DisposeAsync(cause);
        }

        public void Handle(IMessage message) => this.messagingChannel.Handle(new MessageWithFeedback(message, new MessageFeedbackChannel(message.Id, this.messagingClient)));

        public void Close(Exception cause) => this.messagingChannel.Close(cause);

        public event EventHandler CapabilitiesChanged;
    }
}