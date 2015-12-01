// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Auth
{
    public sealed class AuthenticationResult
    {
        static readonly AuthenticationResult FailedResult = new AuthenticationResult
        {
            IsSuccessful = false
        };

        public static AuthenticationResult Failure()
        {
            return FailedResult;
        }

        public static AuthenticationResult SuccessWithSasToken(Identity identity, string token)
        {
            return new AuthenticationResult
            {
                IsSuccessful = true,
                Scope = AuthenticationScope.SasToken,
                Secret = token,
                Identity = identity
            };
        }

        public static AuthenticationResult SuccessWithHubKey(Identity identity, string keyName, string keyValue)
        {
            return new AuthenticationResult
            {
                IsSuccessful = true,
                Identity = identity,
                Scope = AuthenticationScope.HubKey,
                PolicyName = keyName,
                Secret = keyValue
            };
        }

        public static AuthenticationResult SuccessWithDeviceKey(Identity identity, string keyValue)
        {
            return new AuthenticationResult
            {
                IsSuccessful = true,
                Identity = identity,
                Scope = AuthenticationScope.DeviceKey,
                Secret = keyValue
            };
        }

        public static AuthenticationResult SuccessWithDefaultCredentials(Identity identity)
        {
            return new AuthenticationResult
            {
                IsSuccessful = true,
                Identity = identity,
                Scope = AuthenticationScope.None
            };
        }

        AuthenticationResult()
        {
        }

        public bool IsSuccessful { get; private set; }

        public string PolicyName { get; private set; }
        
        public string Secret { get; private set; }

        public AuthenticationScope Scope { get; private set; }
        
        public Identity Identity { get; private set; }
    }
}