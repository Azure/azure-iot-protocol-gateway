// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.Gateway.Core.Mqtt
{
    using System;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Mqtt.Packets;
    using DotNetty.Transport.Channels;

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

        public static string DeriveTopicName(Message message, Settings config)
        {
            string topicName;
            object topicNameValue;
            if (!message.Properties.TryGetValue(config.TopicNameProperty, out topicNameValue))
            {
                topicName = config.DefaultPublishToClientTopicName;
            }
            else
            {
                topicName = (string)topicNameValue;
            }
            return topicName;
        }

        public static QualityOfService DeriveQoS(Message message, Settings config)
        {
            QualityOfService qos;
            object qosValue;
            if (message.Properties.TryGetValue(config.QoSProperty, out qosValue))
            {
                int qosAsInt;
                if (int.TryParse((string)qosValue, out qosAsInt))
                {
                    qos = (QualityOfService)qosAsInt;
                    if (qos > QualityOfService.ExactlyOnce)
                    {
                        BridgeEventSource.Log.Warning(string.Format("Message defined QoS '{0}' is not supported. Downgrading to default value of '{1}'", qos, config.DefaultPublishToClientQoS));
                        qos = config.DefaultPublishToClientQoS;
                    }
                }
                else
                {
                    BridgeEventSource.Log.Warning(string.Format("Message defined QoS '{0}' could not be parsed. Resorting to default value of '{1}'", qosValue, config.DefaultPublishToClientQoS));
                    qos = config.DefaultPublishToClientQoS;
                }
            }
            else
            {
                qos = config.DefaultPublishToClientQoS;
            }
            return qos;
        }

        public static Message ConvertPacketToMessage(PublishPacket packet, Settings settings)
        {
            var message = new Message(packet.Payload.ToArray()); // todo: allocation + copy: recyclable Stream impl over ByteBuf to avoid copying
            message.MessageId = Guid.NewGuid().ToString("N");
            if (packet.Retain)
            {
                message.Properties[settings.RetainProperty] = IotHubTrueString;
            }
            if (packet.Duplicate)
            {
                message.Properties[settings.DupProperty] = IotHubTrueString;
            }

            // todo: validate topic name
            message.Properties[settings.TopicNameProperty] = packet.TopicName;
            return message;
        }

        public static PublishPacket ComposePublishPacket(IChannelHandlerContext context, QualityOfService qos, Message message, string topicName)
        {
            bool duplicate = false; // todo: base on message.DeliveryCount

            var packet = new PublishPacket(qos, duplicate, false);
            packet.TopicName = topicName;
            byte[] messageBytes = message.GetBytes();
            packet.Payload = new UnpooledHeapByteBuffer(context.Channel.Allocator, messageBytes, messageBytes.Length); // todo: proper async streaming of payload (current might block for big messages)
            return packet;
        }
    }
}