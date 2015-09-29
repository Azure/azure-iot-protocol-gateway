// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Mqtt
{
    using System;
    using DotNetty.Codecs.Mqtt.Packets;
    using DotNetty.Common;
    using Microsoft.Azure.Devices.Client;

    sealed class AckPendingMessageState : IPacketReference, ISupportRetransmission // todo: recycle?
    {
        public AckPendingMessageState(Message message, PublishPacket packet)
            : this(message.MessageId, packet.PacketId, packet.QualityOfService, message.LockToken)
        {
        }

        public AckPendingMessageState(string messageId, int packetId, QualityOfService qualityOfService, string lockToken)
        {
            this.MessageId = messageId;
            this.PacketId = packetId;
            this.QualityOfService = qualityOfService;
            this.LockToken = lockToken;
            this.SentTime = DateTime.UtcNow;
            this.StartTimestamp = PreciseTimeSpan.FromStart;
        }

        public PreciseTimeSpan StartTimestamp { get; set; }

        public string MessageId { get; private set; }

        public int PacketId { get; private set; }

        public string LockToken { get; private set; }

        public DateTime SentTime { get; private set; }

        public QualityOfService QualityOfService { get; private set; }

        public void Reset(Message message)
        {
            if (message.MessageId != this.MessageId)
            {
                throw new InvalidOperationException(string.Format(
                    "Expected to receive message with id of {0} but saw a message with id of {1}. Protocol Gateway only supports exclusive connection to IoT Hub.",
                    this.MessageId, message.MessageId));
            }

            this.LockToken = message.LockToken;
            this.SentTime = DateTime.UtcNow;
        }

        public void ResetMessage(Message message)
        {
            if (message.MessageId != this.MessageId)
            {
                throw new InvalidOperationException(string.Format(
                    "Expected to receive message with id of {0} but saw a message with id of {1}. Protocol Gateway only supports exclusive connection to IoT Hub.",
                    this.MessageId, message.MessageId));
            }

            this.LockToken = message.LockToken;
        }

        public void ResetSentTime()
        {
            this.SentTime = DateTime.UtcNow;
        }
    }
}