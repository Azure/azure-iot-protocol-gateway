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

        public static AuthenticationResult SuccessWithSasToken(string deviceId, string token)
        {
            return new AuthenticationResult
            {
                IsSuccessful = true,
                DeviceId = deviceId,
                Scope = AuthenticationScope.SasToken,
                Secret = token
            };
        }

        public static AuthenticationResult SuccessWithHubKey(string deviceId, string keyName, string keyValue)
        {
            return new AuthenticationResult
            {
                IsSuccessful = true,
                DeviceId = deviceId,
                Scope = AuthenticationScope.HubKey,
                PolicyName = keyName,
                Secret = keyValue
            };
        }

        public static AuthenticationResult SuccessWithDeviceKey(string deviceId, string keyValue)
        {
            return new AuthenticationResult
            {
                IsSuccessful = true,
                DeviceId = deviceId,
                Scope = AuthenticationScope.DeviceKey,
                Secret = keyValue
            };
        }

        public static AuthenticationResult SuccessWithDefaultCredentials(string deviceId)
        {
            return new AuthenticationResult
            {
                IsSuccessful = true,
                DeviceId = deviceId,
                Scope = AuthenticationScope.None
            };
        }

        AuthenticationResult()
        {
        }

        public bool IsSuccessful { get; private set; }

        public string DeviceId { get; private set; }

        public string PolicyName { get; private set; }
        
        public string Secret { get; private set; }

        public AuthenticationScope Scope { get; private set; }
    }
}