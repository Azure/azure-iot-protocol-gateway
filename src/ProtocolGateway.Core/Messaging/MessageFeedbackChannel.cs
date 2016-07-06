// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Messaging
{
    using System.Diagnostics.Contracts;
    using System.Threading.Tasks;

    public struct MessageFeedbackChannel
    {
        readonly string messageId;
        readonly IMessagingServiceClient client;

        public MessageFeedbackChannel(string messageId, IMessagingServiceClient client)
        {
            this.messageId = messageId;
            this.client = client;
        }

        [Pure]
        public Task AbandonAsync() => this.client.AbandonAsync(this.messageId);

        [Pure]
        public Task CompleteAsync() => this.client.CompleteAsync(this.messageId);

        [Pure]
        public Task RejectAsync() => this.client.RejectAsync(this.messageId);
    }
}