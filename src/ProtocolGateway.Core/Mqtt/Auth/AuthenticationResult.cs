// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Auth
{
    public sealed class AuthenticationProperties
    {
        public static AuthenticationProperties SuccessWithSasToken(string token)
        {
            return new AuthenticationProperties
            {
                Scope = AuthenticationScope.SasToken,
                Secret = token,
            };
        }

        public static AuthenticationProperties SuccessWithHubKey(string keyName, string keyValue)
        {
            return new AuthenticationProperties
            {
                Scope = AuthenticationScope.HubKey,
                PolicyName = keyName,
                Secret = keyValue
            };
        }

        public static AuthenticationProperties SuccessWithDeviceKey(string keyValue)
        {
            return new AuthenticationProperties
            {
                Scope = AuthenticationScope.DeviceKey,
                Secret = keyValue
            };
        }

        public static AuthenticationProperties SuccessWithDefaultCredentials()
        {
            return new AuthenticationProperties
            {
                Scope = AuthenticationScope.None
            };
        }

        AuthenticationProperties()
        {
        }

        public string PolicyName { get; private set; }
        
        public string Secret { get; private set; }

        public AuthenticationScope Scope { get; private set; }
    }

    public sealed class AuthenticationResult
    {
        public static AuthenticationResult SuccessWithSasToken(Identity identity, string token)
        {
            return new AuthenticationResult
            {
                Properties = AuthenticationProperties.SuccessWithSasToken(token),
                Identity = identity
            };
        }

        public static AuthenticationResult SuccessWithHubKey(Identity identity, string keyName, string keyValue)
        {
            return new AuthenticationResult
            {
                Identity = identity,
                Properties = AuthenticationProperties.SuccessWithHubKey(keyName, keyValue)
            };
        }

        public static AuthenticationResult SuccessWithDeviceKey(Identity identity, string keyValue)
        {
            return new AuthenticationResult
            {
                Identity = identity,
                Properties = AuthenticationProperties.SuccessWithDeviceKey(keyValue)
            };
        }

        public static AuthenticationResult SuccessWithDefaultCredentials(Identity identity)
        {
            return new AuthenticationResult
            {
                Identity = identity,
                Properties = AuthenticationProperties.SuccessWithDefaultCredentials()
            };
        }

        AuthenticationResult()
        {
        }

        public AuthenticationProperties Properties { get; private set; }
        
        public Identity Identity { get; private set; }
    }
}