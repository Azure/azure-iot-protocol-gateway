// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Mqtt
{
    using DotNetty.Common;
    using Microsoft.Azure.Devices.ProtocolGateway.Messaging;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence;

    sealed class CompletionPendingMessageState : IPacketReference
    {
        public CompletionPendingMessageState(int packetId, IQos2MessageDeliveryState deliveryState,
            PreciseTimeSpan startTimestamp, MessageFeedbackChannel feedbackChannel)
        {
            this.PacketId = packetId;
            this.DeliveryState = deliveryState;
            this.StartTimestamp = startTimestamp;
            this.FeedbackChannel = feedbackChannel;
        }

        public int PacketId { get; }

        public IQos2MessageDeliveryState DeliveryState { get; private set; }

        public PreciseTimeSpan StartTimestamp { get; private set; }

        public MessageFeedbackChannel FeedbackChannel { get; set; }
    }
}