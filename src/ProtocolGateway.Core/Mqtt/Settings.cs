// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Mqtt
{
    using System;
    using DotNetty.Codecs.Mqtt.Packets;

    public class Settings
    {
        const string ConnectArrivalTimeoutSetting = "ConnectArrivalTimeout"; // timeout to close the network connection in absence of CONNECT packet
        const string MaxPendingInboundAcknowledgementsSetting = "MaxPendingInboundAcknowledgements"; // number of messages after which driver stops reading from network. Reading from network will resume once one of the accepted messages is completely processed.
        const string MaxKeepAliveTimeoutSetting = "MaxKeepAliveTimeout";
        const string DefaultPublishToClientQoSSetting = "DefaultPublishToClientQoS";
        const string RetainPropertyNameSetting = "RetainPropertyName";
        const string DupPropertyNameSetting = "DupPropertyName";
        const string QoSPropertyNameSetting = "QoSPropertyName";
        const string DeviceReceiveAckTimeoutSetting = "DeviceReceiveAckTimeout";
        const string MaxOutboundRetransmissionCountSetting = "MaxOutboundRetransmissionCount";
        const string ServicePropertyPrefixSetting = "ServicePropertyPrefix";

        const string RetainPropertyNameDefaultValue = "mqtt-retain";
        const string DupPropertyNameDefaultValue = "mqtt-dup";
        const string QoSPropertyNameDefaultValue = "mqtt-qos";
        const int MaxPendingInboundAcknowledgementsDefaultValue = 16;
        const int NoMaxOutboundRetransmissionCountValue = -1;

        readonly TimeSpan? deviceReceiveAckTimeout;

        public Settings(ISettingsProvider settingsProvider)
        {
            int inboundMessages;
            if (!settingsProvider.TryGetIntegerSetting(MaxPendingInboundAcknowledgementsSetting, out inboundMessages) || inboundMessages <= 0)
            {
                inboundMessages = MaxPendingInboundAcknowledgementsDefaultValue;
            }
            this.MaxPendingInboundAcknowledgements = Math.Min(inboundMessages, ushort.MaxValue); // reflects packet id domain per MQTT spec.

            TimeSpan timeout;
            this.ConnectArrivalTimeout = settingsProvider.TryGetTimeSpanSetting(ConnectArrivalTimeoutSetting, out timeout) && timeout > TimeSpan.Zero
                ? (TimeSpan?)timeout
                : null;

            this.DefaultPublishToClientQoS = (QualityOfService)settingsProvider.GetIntegerSetting(DefaultPublishToClientQoSSetting, (int)QualityOfService.AtLeastOnce);

            this.MaxKeepAliveTimeout = settingsProvider.TryGetTimeSpanSetting(MaxKeepAliveTimeoutSetting, out timeout)
                ? timeout
                : (TimeSpan?)null;

            this.RetainPropertyName = settingsProvider.GetSetting(RetainPropertyNameSetting, RetainPropertyNameDefaultValue);
            this.DupPropertyName = settingsProvider.GetSetting(DupPropertyNameSetting, DupPropertyNameDefaultValue);
            this.QoSPropertyName = settingsProvider.GetSetting(QoSPropertyNameSetting, QoSPropertyNameDefaultValue);

            this.deviceReceiveAckTimeout = settingsProvider.TryGetTimeSpanSetting(DeviceReceiveAckTimeoutSetting, out timeout) && timeout > TimeSpan.Zero
                ? timeout
                : (TimeSpan?)null;

            int retransmissionCount;
            if (!settingsProvider.TryGetIntegerSetting(MaxOutboundRetransmissionCountSetting, out retransmissionCount)
                || (retransmissionCount < 0))
            {
                retransmissionCount = NoMaxOutboundRetransmissionCountValue;
            }
            this.MaxOutboundRetransmissionCount = retransmissionCount;

            this.ServicePropertyPrefix = settingsProvider.GetSetting(ServicePropertyPrefixSetting, string.Empty);
        }

        public int MaxPendingInboundAcknowledgements { get; private set; }

        public TimeSpan? ConnectArrivalTimeout { get; private set; }

        public QualityOfService DefaultPublishToClientQoS { get; private set; }

        public TimeSpan? MaxKeepAliveTimeout { get; private set; }

        public string RetainPropertyName { get; }

        public string DupPropertyName { get; }

        public string QoSPropertyName { get; }

        /// <summary>
        ///     When null, there is no limit on delay between sending PUBLISH to client and receiving PUBACK from the client
        /// </summary>
        public TimeSpan DeviceReceiveAckTimeout => this.deviceReceiveAckTimeout ?? TimeSpan.MinValue;

        public bool DeviceReceiveAckCanTimeout => this.deviceReceiveAckTimeout.HasValue;

        public bool MaxOutboundRetransmissionEnforced => this.MaxOutboundRetransmissionCount > NoMaxOutboundRetransmissionCountValue;

        public int MaxOutboundRetransmissionCount { get; }

        public string ServicePropertyPrefix { get; private set; }
    }
}