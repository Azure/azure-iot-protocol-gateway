// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Auth
{
    using System.Security.Principal;

    public sealed class IoTHubIdentity : IIdentity
    {
        public const string AuthenticationTypeName = "IoTHubIdentity";

        public string Name { get; private set; }

        public string AuthenticationType
        {
            get { return AuthenticationTypeName; }
        }

        public bool IsAuthenticated { get; private set; }

        public string IoTHubHostName { get; private set; }

        public string DeviceId { get; private set; }

        public IoTHubIdentity(string iotHubHostName, string deviceId, bool isAuthenticated)
        {
            this.IoTHubHostName = iotHubHostName;
            this.DeviceId = deviceId;
            this.Name = deviceId;
            this.IsAuthenticated = isAuthenticated;
        }
    }
}