// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.IotHubClient
{
    using System;
    using DotNetty.Codecs.Mqtt.Packets;

    public class IotHubClientSettings
    {
        const string SettingPrefix = "IotHubClient.";
        const string IotHubConnectionStringSetting = SettingPrefix + "ConnectionString"; // connection string to IoT Hub. Credentials can be overriden by device specific credentials coming from auth provider
        const string MaxPendingInboundMessagesSetting = SettingPrefix + "MaxPendingInboundMessages"; // number of messages after which driver stops reading from network. Reading from network will resume once one of the accepted messages is completely processed.
        const string MaxPendingOutboundMessagesSetting = SettingPrefix + "MaxPendingOutboundMessages"; // number of messages in flight after which driver stops receiving messages from IoT Hub queue. Receiving will resume once one of the messages is completely processed.
        const string DefaultPublishToClientQoSSetting = SettingPrefix + "DefaultPublishToClientQoS";
        const string MaxOutboundRetransmissionCountSetting = SettingPrefix + "MaxOutboundRetransmissionCount";
        const string PassThroughUnmatchedMessagesSetting = SettingPrefix + "PassThroughUnmatchedMessages";
        const string ServicePropertyPrefixSetting = SettingPrefix + "ServicePropertyPrefix";

        const int MaxPendingInboundMessagesDefaultValue = 16;
        const int MaxPendingOutboundMessagesDefaultValue = 1;
        const int NoMaxOutboundRetransmissionCountValue = -1;

        public IotHubClientSettings(ISettingsProvider settingsProvider)
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
            this.MaxPendingOutboundMessages = Math.Min(outboundMessages, ushort.MaxValue >> 2); // limited due to separation of packet id domains for QoS 1 and 2.

            int qos;
            this.DefaultPublishToClientQoS = settingsProvider.TryGetIntegerSetting(DefaultPublishToClientQoSSetting, out qos)
                ? (QualityOfService)qos
                : QualityOfService.AtLeastOnce;

            string connectionString = settingsProvider.GetSetting(IotHubConnectionStringSetting);
            if (connectionString.IndexOf("DeviceId=", StringComparison.OrdinalIgnoreCase) == -1)
            {
                connectionString += ";DeviceId=stub";
            }
            this.IotHubConnectionString = connectionString;

            int retransmissionCount;
            if (!settingsProvider.TryGetIntegerSetting(MaxOutboundRetransmissionCountSetting, out retransmissionCount)
                || (retransmissionCount < 0))
            {
                retransmissionCount = NoMaxOutboundRetransmissionCountValue;
            }
            this.MaxOutboundRetransmissionCount = retransmissionCount;

            this.PassThroughUnmatchedMessages = settingsProvider.GetBooleanSetting(PassThroughUnmatchedMessagesSetting, false);

            this.ServicePropertyPrefix = settingsProvider.GetSetting(ServicePropertyPrefixSetting, string.Empty);
        }

        public int MaxPendingInboundMessages { get; private set; }

        public int MaxPendingOutboundMessages { get; private set; }

        public QualityOfService DefaultPublishToClientQoS { get; private set; }

        public string IotHubConnectionString { get; private set; }

        public bool MaxOutboundRetransmissionEnforced => this.MaxOutboundRetransmissionCount > NoMaxOutboundRetransmissionCountValue;

        public int MaxOutboundRetransmissionCount { get; }

        public bool PassThroughUnmatchedMessages { get; private set; }

        public string ServicePropertyPrefix { get; private set; }
    }
}