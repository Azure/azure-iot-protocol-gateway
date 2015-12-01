// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Auth
{
    using System;

    public sealed class Identity
    {
        public string IoTHubHostName { get; private set; }

        public string DeviceId { get; private set; }

        public Identity(string iotHubHostName, string deviceId)
        {
            this.IoTHubHostName = iotHubHostName;
            this.DeviceId = deviceId;
        }

        public override string ToString()
        {
            return this.IoTHubHostName + "/" + this.DeviceId;
        }

        public static Identity Parse(string username)
        {
            int delimiterPos = username.IndexOf("/", StringComparison.Ordinal);
            if (delimiterPos < 0)
            {
                throw new FormatException(string.Format("Invalid username format: {0}", username));
            }
            string iotHubName = username.Substring(0, delimiterPos);
            string deviceId = username.Substring(delimiterPos + 1);

            return new Identity(iotHubName, deviceId);
        }
    }
}