// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Messaging
{
    using System;
    using System.Threading.Tasks;

    public sealed class SingleClientMqttMessagingBridge : IMqttMessagingBridge, IMessagingChannel<IMessage>
    {
        readonly IMessagingServiceClient messagingClient;
        IMessagingChannel<MessageWithFeedback> messagingChannel;

        public SingleClientMqttMessagingBridge(IMessagingServiceClient messagingClient)
        {
            this.messagingClient = messagingClient;
        }

        public void BindMessagingChannel(IMessagingChannel<MessageWithFeedback> channel)
        {
            this.messagingChannel = channel;
            this.messagingClient.BindMessagingChannel(this);
        }

        public bool TryResolveClient(string topicName, out IMessagingServiceClient sendingClient)
        {
            sendingClient = this.messagingClient;
            return true;
        }

        public Task DisposeAsync()
        {
            return this.messagingClient.DisposeAsync();
        }

        public void Handle(IMessage message) => this.messagingChannel.Handle(new MessageWithFeedback(message, new MessageFeedbackChannel(message.Id, this.messagingClient)));

        public void Close(Exception exception) => this.messagingChannel.Close(exception); // todo: wrap exception?
    }
}