// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.IotHubClient
{
    using Microsoft.Azure.Devices.ProtocolGateway.Identity;

    public sealed class IotHubDeviceIdentity : IDeviceIdentity
    {
        string asString;
        string policyName;
        AuthenticationScope scope;

        public IotHubDeviceIdentity(string iotHubHostName, string deviceId)
        {
            this.IotHubHostName = iotHubHostName;
            this.Id = deviceId;
        }

        public string Id { get; }

        public bool IsAuthenticated => true;

        public string IotHubHostName { get; }

        public string PolicyName
        {
            get { return this.policyName; }
            private set
            {
                this.policyName = value;
                this.asString = null;
            }
        }

        public string Secret { get; private set; }

        public AuthenticationScope Scope
        {
            get { return this.scope; }
            private set
            {
                this.scope = value;
                this.asString = null;
            }
        }

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

        public void WithHubKey(string keyName, string keyValue)
        {
            this.Scope = AuthenticationScope.HubKey;
            this.PolicyName = keyName;
            this.Secret = keyValue;
        }

        public void WithDeviceKey(string keyValue)
        {
            this.Scope = AuthenticationScope.DeviceKey;
            this.Secret = keyValue;
        }

        public override string ToString()
        {
            if (this.asString == null)
            {
                string policy = string.IsNullOrEmpty(this.PolicyName) ? "<none>" : this.PolicyName;
                this.asString = $"{this.Id} [IotHubHostName: {this.IotHubHostName}; PolicyName: {policy}; Scope: {this.Scope}]";
            }
            return this.asString;
        }
    }
}