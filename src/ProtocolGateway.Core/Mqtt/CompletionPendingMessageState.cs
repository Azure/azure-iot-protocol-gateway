// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Mqtt
{
    using System;
    using DotNetty.Common;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence;

    sealed class CompletionPendingMessageState : IPacketReference, ISupportRetransmission
    {
        public CompletionPendingMessageState(int packetId, string lockToken,
             IQos2MessageDeliveryState deliveryState, PreciseTimeSpan startTimestamp)
        {
            this.PacketId = packetId;
            this.LockToken = lockToken;
            this.DeliveryState = deliveryState;
            this.StartTimestamp = startTimestamp;
            this.SentTime = DateTime.UtcNow;
        }

        public int PacketId { get; private set; }

        public string LockToken { get; private set; }

        public IQos2MessageDeliveryState DeliveryState { get; private set; }

        public PreciseTimeSpan StartTimestamp { get; private set; }

        public DateTime SentTime { get; private set; }

        public void ResetSentTime()
        {
            this.SentTime = DateTime.UtcNow;
        }
    }
}