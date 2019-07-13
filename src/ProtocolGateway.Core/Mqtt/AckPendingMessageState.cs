// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Mqtt
{
    using DotNetty.Codecs.Mqtt.Packets;
    using DotNetty.Common;
    using Microsoft.Azure.Devices.ProtocolGateway.Messaging;

    sealed class AckPendingMessageState : IPacketReference // todo: recycle?
    {
        public AckPendingMessageState(IMessage message, IMessagingSource callback, PublishPacket packet)
        {
            this.SequenceNumber = message.SequenceNumber;
            this.PacketId = packet.PacketId;
            this.QualityOfService = packet.QualityOfService;
            this.FeedbackChannel = new MessageFeedbackChannel(message.Id, callback);
            this.StartTimestamp = PreciseTimeSpan.FromStart;
        }

        public PreciseTimeSpan StartTimestamp { get; set; }

        public ulong SequenceNumber { get; }

        public int PacketId { get; }

        public QualityOfService QualityOfService { get; private set; }

        public MessageFeedbackChannel FeedbackChannel { get; private set; }
    }
}