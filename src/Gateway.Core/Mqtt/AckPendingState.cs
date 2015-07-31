// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.Gateway.Core.Mqtt
{
    using System;
    using DotNetty.Codecs.Mqtt.Packets;

    class AckPendingState // todo: recycle
    {
        public AckPendingState(string messageId, int packetId, QualityOfService qos, string topicName, string lockToken)
        {
            this.MessageId = messageId;
            this.PacketId = packetId;
            this.QualityOfService = qos;
            this.TopicName = topicName;
            this.LockToken = lockToken;
            this.PublishTime = DateTime.UtcNow;
        }

        public string MessageId { get; private set; }

        public int PacketId { get; private set; }

        public string LockToken { get; private set; }

        public DateTime PublishTime { get; private set; }

        public QualityOfService QualityOfService { get; private set; }

        public string TopicName { get; private set; }

        public void Reset(string lockToken)
        {
            this.LockToken = lockToken;
            this.PublishTime = DateTime.UtcNow;
        }
    }
}