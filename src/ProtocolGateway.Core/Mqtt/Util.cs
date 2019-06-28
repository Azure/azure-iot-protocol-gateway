// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Mqtt
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Mqtt.Packets;
    using DotNetty.Transport.Channels;
    using Microsoft.Azure.Devices.ProtocolGateway.Instrumentation;
    using Microsoft.Azure.Devices.ProtocolGateway.Messaging;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence;

    static class Util
    {
        const char SegmentSeparatorChar = '/';
        const char SingleSegmentWildcardChar = '+';
        const char MultiSegmentWildcardChar = '#';
        static readonly char[] WildcardChars = { MultiSegmentWildcardChar, SingleSegmentWildcardChar };
        const string IotHubTrueString = "true";

        public static bool CheckTopicFilterMatch(string topicName, string topicFilter)
        {
            int topicFilterIndex = 0;
            int topicNameIndex = 0;
            while (topicNameIndex < topicName.Length && topicFilterIndex < topicFilter.Length)
            {
                int wildcardIndex = topicFilter.IndexOfAny(WildcardChars, topicFilterIndex);
                if (wildcardIndex == -1)
                {
                    int matchLength = Math.Max(topicFilter.Length - topicFilterIndex, topicName.Length - topicNameIndex);
                    return string.Compare(topicFilter, topicFilterIndex, topicName, topicNameIndex, matchLength, StringComparison.Ordinal) == 0;
                }
                else
                {
                    if (topicFilter[wildcardIndex] == MultiSegmentWildcardChar)
                    {
                        if (wildcardIndex == 0) // special case -- any topic name would match
                        {
                            return true;
                        }
                        else
                        {
                            int matchLength = wildcardIndex - topicFilterIndex - 1;
                            if (string.Compare(topicFilter, topicFilterIndex, topicName, topicNameIndex, matchLength, StringComparison.Ordinal) == 0
                                && (topicName.Length == topicNameIndex + matchLength || (topicName.Length > topicNameIndex + matchLength && topicName[topicNameIndex + matchLength] == SegmentSeparatorChar)))
                            {
                                // paths match up till wildcard and either it is parent topic in hierarchy (one level above # specified) or any child topic under a matching parent topic
                                return true;
                            }
                            else
                            {
                                return false;
                            }
                        }
                    }
                    else
                    {
                        // single segment wildcard
                        int matchLength = wildcardIndex - topicFilterIndex;
                        if (matchLength > 0 && string.Compare(topicFilter, topicFilterIndex, topicName, topicNameIndex, matchLength, StringComparison.Ordinal) != 0)
                        {
                            return false;
                        }
                        topicNameIndex = topicName.IndexOf(SegmentSeparatorChar, topicNameIndex + matchLength);
                        topicFilterIndex = wildcardIndex + 1;
                        if (topicNameIndex == -1)
                        {
                            // there's no more segments following matched one
                            return topicFilterIndex == topicFilter.Length;
                        }
                    }
                }
            }

            return topicFilterIndex == topicFilter.Length && topicNameIndex == topicName.Length;
        }

        public static QualityOfService DeriveQos(IMessage message, Settings config, string channelId, string deviceId)
        {
            string qosValue;
            if (!message.Properties.TryGetValue(config.QoSPropertyName, out qosValue))
            {
                return config.DefaultPublishToClientQoS;
            }

            if (qosValue.Length > 1)
            {
                CommonEventSource.Log.Warning($"Message defined QoS '{qosValue}' is not supported. Downgrading to default value of '{config.DefaultPublishToClientQoS}'", channelId, deviceId);
                return config.DefaultPublishToClientQoS;
            }

            switch (qosValue[0])
            {
                case '0':
                    return QualityOfService.AtMostOnce;
                case '1':
                    return QualityOfService.AtLeastOnce;
                case '2':
                    return QualityOfService.ExactlyOnce;
                default:
                    CommonEventSource.Log.Warning($"Message defined QoS '{qosValue}' is not supported. Downgrading to default value of '{config.DefaultPublishToClientQoS}'", channelId, deviceId);
                    return config.DefaultPublishToClientQoS;
            }
        }

        public static IMessage CompleteMessageFromPacket(IMessage message, PublishPacket packet, Settings settings)
        {
            if (packet.RetainRequested)
            {
                message.Properties[settings.RetainPropertyName] = IotHubTrueString;
            }
            if (packet.Duplicate)
            {
                message.Properties[settings.DupPropertyName] = IotHubTrueString;
            }

            return message;
        }

        public static PublishPacket ComposePublishPacket(IChannelHandlerContext context, IMessage message,
            QualityOfService qos, IByteBufferAllocator allocator)
        {
            bool duplicate = message.DeliveryCount > 0;

            var packet = new PublishPacket(qos, duplicate, false);
            packet.TopicName = message.Address;
            if (qos > QualityOfService.AtMostOnce)
            {
                int packetId = unchecked((int)message.SequenceNumber) & 0x3FFF; // clear bits #14 and #15
                switch (qos)
                {
                    case QualityOfService.AtLeastOnce:
                        break;
                    case QualityOfService.ExactlyOnce:
                        packetId |= 0x8000; // set bit #15
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(qos), qos, null);
                }
                packet.PacketId = packetId + 1;
            }
            message.Payload.Retain();
            packet.Payload = message.Payload;
            return packet;
        }

        public static SubAckPacket AddSubscriptions(ISessionState session, SubscribePacket packet, QualityOfService maxSupportedQos)
        {
            IReadOnlyList<ISubscription> subscriptions = session.Subscriptions;
            var returnCodes = new List<QualityOfService>(subscriptions.Count);
            foreach (SubscriptionRequest request in packet.Requests)
            {
                QualityOfService finalQos = request.QualityOfService < maxSupportedQos ? request.QualityOfService : maxSupportedQos;

                session.AddOrUpdateSubscription(request.TopicFilter, finalQos);

                returnCodes.Add(finalQos);
            }
            var ack = new SubAckPacket
            {
                PacketId = packet.PacketId,
                ReturnCodes = returnCodes
            };
            return ack;
        }

        public static UnsubAckPacket RemoveSubscriptions(ISessionState session, UnsubscribePacket packet)
        {
            foreach (string topicToRemove in packet.TopicFilters)
            {
                session.RemoveSubscription(topicToRemove);
            }
            var ack = new UnsubAckPacket
            {
                PacketId = packet.PacketId
            };
            return ack;
        }

        public static async Task WriteMessageAsync(IChannelHandlerContext context, object message)
        {
            await context.WriteAndFlushAsync(message);
            if (message is PublishPacket)
            {
                PerformanceCounters.PublishPacketsSentPerSecond.Increment();
            }
            PerformanceCounters.PacketsSentPerSecond.Increment();
        }
    }
}