// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Messaging
{
    public struct MessageWithFeedback
    {
        public readonly IMessage Message;
        public readonly MessageFeedbackChannel FeedbackChannel;

        public MessageWithFeedback(IMessage message, MessageFeedbackChannel feedbackChannel)
        {
            this.Message = message;
            this.FeedbackChannel = feedbackChannel;
        }
    }
}