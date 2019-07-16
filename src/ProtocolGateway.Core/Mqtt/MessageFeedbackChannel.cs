// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Messaging
{
    using System.Threading.Tasks;

    public struct MessageFeedbackChannel
    {
        readonly string messageId;
        readonly IMessagingSource callback;

        public MessageFeedbackChannel(string messageId, IMessagingSource callback)
        {
            this.messageId = messageId;
            this.callback = callback;
        }

        public Task CompleteAsync() => this.callback.CompleteAsync(this.messageId);

        public Task RejectAsync() => this.callback.RejectAsync(this.messageId);
    }
}