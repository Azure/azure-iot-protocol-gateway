// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Mqtt
{
    using System;
    using DotNetty.Codecs.Mqtt.Packets;

    public class Settings
    {
        const string IotHubConnectionStringSetting = "IoTHubConnectionString"; // connection string to IoT Hub. Credentials can be overriden by device specific credentials coming from auth provider
        const string ConnectArrivalTimeoutSetting = "ConnectArrivalTimeout"; // timeout to close the network connection in absence of CONNECT packet
        const string MaxPendingInboundMessagesSetting = "MaxPendingInboundMessages"; // number of messages after which driver stops reading from network. Reading from network will resume once one of the accepted messages is completely processed.
        const string MaxPendingOutboundMessagesSetting = "MaxPendingOutboundMessages"; // number of messages in flight after which driver stops receiving messages from IoT Hub queue. Receiving will resume once one of the messages is completely processed.
        const string MaxKeepAliveTimeoutSetting = "MaxKeepAliveTimeout";
        const string DefaultPublishToClientQoSSetting = "DefaultPublishToClientQoS";
        const string RetainPropertyNameSetting = "RetainPropertyName";
        const string DupPropertyNameSetting = "DupPropertyName";
        const string QoSPropertyNameSetting = "QoSPropertyName";
        const string DeviceReceiveAckTimeoutSetting = "DeviceReceiveAckTimeout";
        const string MaxOutboundRetransmissionCountSetting = "MaxOutboundRetransmissionCount";
        const string FailOnUnmatchedIncomingMessageNameSetting = "FailOnUnmatchedIncomingMessage";

        const string RetainPropertyNameDefaultValue = "mqtt-retain";
        const string DupPropertyNameDefaultValue = "mqtt-dup";
        const string QoSPropertyNameDefaultValue = "mqtt-qos";
        const int MaxPendingOutboundMessagesDefaultValue = 1;
        const int MaxPendingInboundMessagesDefaultValue = 16;
        const int MaxOutboundRetransmissionCountDefaultValue = -1;
        const bool FailOnUnmatchedIncomingMessageDefaultValue = false;


        readonly string retainPropertyName;
        readonly string dupPropertyName;
        readonly string qosPropertyName;
        readonly TimeSpan? deviceReceiveAckTimeout;
        readonly bool failOnUnmatchedIncomingMessage;

        public Settings(ISettingsProvider settingsProvider)
        {
            int inboundMessages;
            if (!settingsProvider.TryGetIntegerSetting(MaxPendingInboundMessagesSetting, out inboundMessages) || inboundMessages <= 0)
            {
                inboundMessages = MaxPendingInboundMessagesDefaultValue;
            }
            this.MaxPendingInboundMessages = Math.Min(inboundMessages, ushort.MaxValue); // reflects packet id domain per MQTT spec.

            int outboundMessages;
            if (!settingsProvider.TryGetIntegerSetting(MaxPendingOutboundMessagesSetting, out outboundMessages) || outboundMessages <= 0)
            {
                outboundMessages = MaxPendingOutboundMessagesDefaultValue;
            }
            this.MaxPendingOutboundMessages = Math.Min(outboundMessages, ushort.MaxValue / 2); // limited due to separation of packet id domains for QoS 1 and 2.

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

            if (!settingsProvider.TryGetSetting(RetainPropertyNameSetting, out this.retainPropertyName))
            {
                this.retainPropertyName = RetainPropertyNameDefaultValue;
            }

            if (!settingsProvider.TryGetSetting(DupPropertyNameSetting, out this.dupPropertyName))
            {
                this.dupPropertyName = DupPropertyNameDefaultValue;
            }

            if (!settingsProvider.TryGetSetting(QoSPropertyNameSetting, out this.qosPropertyName))
            {
                this.qosPropertyName = QoSPropertyNameDefaultValue;
            }

            if (!settingsProvider.TryGetBooleanSetting(FailOnUnmatchedIncomingMessageNameSetting, out this.failOnUnmatchedIncomingMessage))
            {
                this.failOnUnmatchedIncomingMessage = FailOnUnmatchedIncomingMessageDefaultValue;
            }

            this.deviceReceiveAckTimeout = settingsProvider.TryGetTimeSpanSetting(DeviceReceiveAckTimeoutSetting, out timeout) && timeout > TimeSpan.Zero
                ? timeout
                : (TimeSpan?)null;

            int retransmissionCount;
            if (!settingsProvider.TryGetIntegerSetting(MaxOutboundRetransmissionCountSetting, out retransmissionCount) || retransmissionCount < MaxOutboundRetransmissionCountDefaultValue)
            {
                retransmissionCount = MaxOutboundRetransmissionCountDefaultValue;
            }
            this.MaxOutboundRetransmissionCount = retransmissionCount;
        }

        public int MaxPendingOutboundMessages { get; }

        public int MaxPendingInboundMessages { get; }

        public TimeSpan? ConnectArrivalTimeout { get; }

        public QualityOfService DefaultPublishToClientQoS { get; }

        public string IotHubConnectionString { get; }

        public TimeSpan? MaxKeepAliveTimeout { get; }

        public string RetainPropertyName => this.retainPropertyName;

        public string DupPropertyName => this.dupPropertyName;

        public string QoSPropertyName => this.qosPropertyName;

        /// <summary>
        ///     When null, there is no limit on delay between sending PUBLISH to client and receiving PUBACK from the client
        /// </summary>
        public TimeSpan DeviceReceiveAckTimeout => this.deviceReceiveAckTimeout ?? TimeSpan.MinValue;

        public bool DeviceReceiveAckCanTimeout => this.deviceReceiveAckTimeout.HasValue;

        public bool MaxOutboundRetransmissionEnforced => this.MaxOutboundRetransmissionCount >= 0;

        public int MaxOutboundRetransmissionCount { get; }

        public bool FailOnUnmatchedIncomingMessage => this.failOnUnmatchedIncomingMessage;
    }
}