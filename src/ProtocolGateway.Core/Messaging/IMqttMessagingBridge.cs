// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Messaging
{
    using System.Threading.Tasks;

    public interface IMqttMessagingBridge
    {
        void BindMessagingChannel(IMessagingChannel<MessageWithFeedback> channel);

        bool TryResolveClient(string topicName, out IMessagingServiceClient sendingClient);

        Task DisposeAsync();
    }
}