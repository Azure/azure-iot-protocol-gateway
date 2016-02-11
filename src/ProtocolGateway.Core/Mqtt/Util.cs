﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Mqtt
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.IO;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Mqtt.Packets;
    using DotNetty.Transport.Channels;
    using Microsoft.Azure.Devices.ProtocolGateway.Instrumentation;
    using Microsoft.Azure.Devices.ProtocolGateway.IotHub;
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
                    return String.Compare(topicFilter, topicFilterIndex, topicName, topicNameIndex, matchLength, StringComparison.Ordinal) == 0;
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

        public static QualityOfService DeriveQos(IMessage message, Settings config)
        {
            QualityOfService qos;
            string qosValue;
            if (message.Properties.TryGetValue(config.QoSPropertyName, out qosValue))
            {
                int qosAsInt;
                if (int.TryParse(qosValue, out qosAsInt))
                {
                    qos = (QualityOfService)qosAsInt;
                    if (qos > QualityOfService.ExactlyOnce)
                    {
                        MqttIotHubAdapterEventSource.Log.Warning($"Message defined QoS '{qos}' is not supported. Downgrading to default value of '{config.DefaultPublishToClientQoS}'");
                        qos = config.DefaultPublishToClientQoS;
                    }
                }
                else
                {
                    MqttIotHubAdapterEventSource.Log.Warning($"Message defined QoS '{qosValue}' could not be parsed. Resorting to default value of '{config.DefaultPublishToClientQoS}'");
                    qos = config.DefaultPublishToClientQoS;
                }
            }
            else
            {
                qos = config.DefaultPublishToClientQoS;
            }
            return qos;
        }

        public static IMessage CompleteMessageFromPacket(IMessage message, PublishPacket packet, Settings settings)
        {
            message.MessageId = Guid.NewGuid().ToString("N");
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

        public static async Task<PublishPacket> ComposePublishPacketAsync(IChannelHandlerContext context, IMessage message,
            QualityOfService qos, string topicName, IByteBufferAllocator allocator)
        {
            bool duplicate = message.DeliveryCount > 0;

            var packet = new PublishPacket(qos, duplicate, false);
            packet.TopicName = topicName;
            if (qos > QualityOfService.AtMostOnce)
            {
                int packetId = message.SequenceNumber;
                switch (qos)
                {
                    case QualityOfService.AtLeastOnce:
                        packetId &= 0x7FFF; // clear 15th bit
                        break;
                    case QualityOfService.ExactlyOnce:
                        packetId |= 0x8000; // set 15th bit
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(qos), qos, null);
                }
                packet.PacketId = packetId;
            }
            using (Stream payloadStream = message.Body)
            {
                long streamLength = payloadStream.Length;
                if (streamLength > int.MaxValue)
                {
                    throw new InvalidOperationException($"Message size ({streamLength} bytes) is too big to process.");
                }

                int length = (int)streamLength;
                IByteBuffer buffer = allocator.Buffer(length, length);
                await buffer.WriteBytesAsync(payloadStream, length);
                Contract.Assert(buffer.ReadableBytes == length);

                packet.Payload = buffer;
            }
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

        public static void AppendMessageContext(IMessage message, IDictionary<string, string> messageContext)
        {
            if (messageContext == null)
            {
                return;
            }

            foreach (KeyValuePair<string, string> property in messageContext)
            {
                message.Properties.Add(property);
            }
        }
    }
}