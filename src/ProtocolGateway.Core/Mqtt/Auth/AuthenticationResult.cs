// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Auth
{
    using System.Security.Principal;

    public sealed class AuthenticationResult
    {
        static readonly AuthenticationResult FailureAuthenticationResult = new AuthenticationResult { Identity = new IotHubIdentity(null, null, false) };

        public static AuthenticationResult Failure => FailureAuthenticationResult;
        
        public static AuthenticationResult SuccessWithSasToken(IIdentity identity, string token)
        {
            return new AuthenticationResult
            {
                Properties = AuthenticationProperties.SuccessWithSasToken(token),
                Identity = identity
            };
        }

        public static AuthenticationResult SuccessWithHubKey(IIdentity identity, string keyName, string keyValue)
        {
            return new AuthenticationResult
            {
                Identity = identity,
                Properties = AuthenticationProperties.SuccessWithHubKey(keyName, keyValue)
            };
        }

        public static AuthenticationResult SuccessWithDeviceKey(IIdentity identity, string keyValue)
        {
            return new AuthenticationResult
            {
                Identity = identity,
                Properties = AuthenticationProperties.SuccessWithDeviceKey(keyValue)
            };
        }

        public static AuthenticationResult SuccessWithDefaultCredentials(IIdentity identity)
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
        
        public IIdentity Identity { get; private set; }
    }
}