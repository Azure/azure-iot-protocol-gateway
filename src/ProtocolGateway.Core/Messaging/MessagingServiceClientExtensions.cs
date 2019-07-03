// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Messaging
{
    public static class MessagingServiceClientExtensions
    {
        public static MessageWithFeedback AttachFeedbackChannel(this IMessagingServiceClient client, IMessage message)
        {
            return new MessageWithFeedback(message, new MessageFeedbackChannel(message.Id, client));
        }
    }
}