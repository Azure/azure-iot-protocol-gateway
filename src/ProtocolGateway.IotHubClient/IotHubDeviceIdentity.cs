// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.IotHubClient
{
    using Microsoft.Azure.Devices.ProtocolGateway.Security;

    public sealed class IotHubDeviceIdentity : IDeviceIdentity
    {
        public IotHubDeviceIdentity(string iotHubHostName, string deviceId)
        {
            this.IotHubHostName = iotHubHostName;
            this.Id = deviceId;
        }

        public string Id { get; }

        public bool IsAuthenticated => true;

        public string IotHubHostName { get; private set; }

        public string PolicyName { get; private set; }

        public string Secret { get; private set; }

        public AuthenticationScope Scope { get; private set; }

        public static bool TryParse(string value, out IotHubDeviceIdentity identity)
        {
            string[] usernameSegments = value.Split('/');
            if (usernameSegments.Length < 2)
            {
                identity = null;
                return false;
            }
            identity = new IotHubDeviceIdentity(usernameSegments[0], usernameSegments[1]);
            return true;
        }

        public void WithSasToken(string token)
        {
            this.Scope = AuthenticationScope.SasToken;
            this.Secret = token;
        }
    }
}