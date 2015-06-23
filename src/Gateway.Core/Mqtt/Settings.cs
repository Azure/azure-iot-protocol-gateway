// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.Gateway.Core.Mqtt
{
    using System;
    using System.Configuration;
    using DotNetty.Codecs.Mqtt.Packets;

    public class Settings
    {
        const string IotHubConnectionStringSetting = "IoTHub.ConnectionString"; // connection string to IoT Hub. Credentials can be overriden by device specific credentials coming from auth provider
        const string ConnectArrivalTimeoutSetting = "ConnectArrivalTimeout"; // timeout to close the network connection in absence of CONNECT packet
        const string NoTelemetryAlertTimeoutSetting = "NoTelemetryAlertTimeout"; // timeout to trace warning for Zombie client (pings happen, no meaningful activity)
        //const string MaxOutstandingInboundMessagesSetting = "MaxOutstandingInboundMessages"; // number of messages after which driver stops reading from network. Reading from network will resume once one of the accepted messages is completely processed.
        const string MaxOutstandingOutboundMessagesSetting = "MaxOutstandingOutboundMessages"; // number of messages in flight after which driver stops receiving messages from IoT Hub queue. Receiving will resume once one of the messages is completely processed.
        const string MaxKeepAliveTimeoutSetting = "MaxKeepAliveTimeout";
        const string DefaultPublishToClientQoSSetting = "DefaultPublishToClientQoS";
        const string DefaultPublishToClientTopicNameSetting = "DefaultPublishToClientTopicName";
        const string RetainPropertySetting = "IoTHub.RetainProperty";
        const string TopicNamePropertySetting = "IoTHub.TopicNameProperty";
        const string DupPropertySetting = "IoTHub.DupProperty";
        const string QoSPropertySetting = "IoTHub.QoSProperty";
        const string WillTopicNameSetting = "IoTHub.WillTopicName";
        const string DeviceReceiveAckTimeoutSetting = "DeviceReceiveAckTimeout";
        const string MaxRetransmissionCountSetting = "MaxRetransmissionCount";

        const string RetainPropertyDefaultValue = "mqtt-retain";
        const string TopicNamePropertyDefaultValue = "mqtt-topic-name";
        const string DupPropertyDefaultValue = "mqtt-dup";
        const string QoSPropertyDefaultValue = "mqtt-qos";
        const string WillTopicNameDefaultValue = "messages/events";
        const int MaxOutstandingOutboundMessagesDefaultValue = 1;
        const int MaxRetransmissionCountDefaultValue = 0;

        readonly string retainProperty;
        readonly string topicNameProperty;
        readonly string dupProperty;
        readonly string qosProperty;
        readonly string willTopicName;
        readonly string defaultPublishToClientTopicName;
        readonly TimeSpan? deviceReceiveAckTimeout;

        public Settings(ISettingsProvider settingsProvider)
        {
            int outboundMessages;
            if (!settingsProvider.TryGetIntegerSetting(MaxOutstandingOutboundMessagesSetting, out outboundMessages) || outboundMessages <= 0)
            {
                outboundMessages = MaxOutstandingOutboundMessagesDefaultValue;
            }
            this.MaxOutstandingOutboundMessages = outboundMessages;

            TimeSpan timeout;
            this.ConnectArrivalTimeout = settingsProvider.TryGetTimeSpanSetting(ConnectArrivalTimeoutSetting, out timeout) && timeout > TimeSpan.Zero
                ? (TimeSpan?)timeout
                : null;

            int qos;
            this.DefaultPublishToClientQoS = settingsProvider.TryGetIntegerSetting(DefaultPublishToClientQoSSetting, out qos)
                ? (QualityOfService)qos
                : QualityOfService.AtLeastOnce;

            this.IotHubConnectionString = settingsProvider.GetSetting(IotHubConnectionStringSetting);

            this.MaxKeepAliveTimeout = settingsProvider.TryGetTimeSpanSetting(MaxKeepAliveTimeoutSetting, out timeout)
                ? timeout
                : (TimeSpan?)null;

            if (!settingsProvider.TryGetSetting(DefaultPublishToClientTopicNameSetting, out this.defaultPublishToClientTopicName))
            {
                throw new ConfigurationErrorsException("");
            }

            if (!settingsProvider.TryGetSetting(RetainPropertySetting, out this.retainProperty))
            {
                this.retainProperty = RetainPropertyDefaultValue;
            }
            if (!settingsProvider.TryGetSetting(TopicNamePropertySetting, out this.topicNameProperty))
            {
                this.topicNameProperty = TopicNamePropertyDefaultValue;
            }
            if (!settingsProvider.TryGetSetting(DupPropertySetting, out this.dupProperty))
            {
                this.dupProperty = DupPropertyDefaultValue;
            }
            if (!settingsProvider.TryGetSetting(QoSPropertySetting, out this.qosProperty))
            {
                this.qosProperty = QoSPropertyDefaultValue;
            }

            if (!settingsProvider.TryGetSetting(WillTopicNameSetting, out this.willTopicName))
            {
                this.willTopicName = WillTopicNameDefaultValue;
            }

            this.deviceReceiveAckTimeout = settingsProvider.TryGetTimeSpanSetting(DeviceReceiveAckTimeoutSetting, out timeout) && timeout > TimeSpan.Zero
                ? timeout
                : (TimeSpan?)null;

            int retransmissionCount;
            if (!settingsProvider.TryGetIntegerSetting(MaxRetransmissionCountSetting, out retransmissionCount) || retransmissionCount < 0)
            {
                retransmissionCount = MaxRetransmissionCountDefaultValue;
            }
            this.MaxRetransmissionCount = retransmissionCount;
        }

        public int MaxOutstandingOutboundMessages { get; private set; }

        public TimeSpan? ConnectArrivalTimeout { get; private set; }

        public QualityOfService DefaultPublishToClientQoS { get; private set; }

        public string IotHubConnectionString { get; private set; }

        public TimeSpan? MaxKeepAliveTimeout { get; private set; }

        public string DefaultPublishToClientTopicName
        {
            get { return this.defaultPublishToClientTopicName; }
        }

        public string RetainProperty
        {
            get { return this.retainProperty; }
        }

        public string TopicNameProperty
        {
            get { return this.topicNameProperty; }
        }

        public string DupProperty
        {
            get { return this.dupProperty; }
        }

        public string QoSProperty
        {
            get { return this.qosProperty; }
        }

        public string WillTopicName
        {
            get { return this.willTopicName; }
        }

        /// <summary>
        /// When null, there is no limit on delay between sending PUBLISH to client and receiving PUBACK from the client
        /// </summary>
        public TimeSpan DeviceReceiveAckTimeout
        {
            get { return this.deviceReceiveAckTimeout.Value; }
        }

        public bool DeviceReceiveAckCanTimeout
        {
            get { return this.deviceReceiveAckTimeout.HasValue; }
        }

        /// <summary>
        /// When set to 0 and <seealso cref="DeviceReceiveAckTimeout"/> is defined, connection must be closed if PUBACK is not received from the client in a given timeframe.
        /// When set to positive value, determines, how many times a message can be retransmitted before closing connection
        /// </summary>
        public int MaxRetransmissionCount { get; private set; }
    }
}