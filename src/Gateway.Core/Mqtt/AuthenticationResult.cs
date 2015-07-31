// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.Gateway.Core.Mqtt
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

        public static AuthenticationResult SuccessWithHubCredentials(string deviceId, string keyName, string keyValue)
        {
            return new AuthenticationResult
            {
                IsSuccessful = true,
                DeviceId = deviceId,
                Scope = AuthenticationScope.Hub,
                KeyName = keyName,
                KeyValue = keyValue
            };
        }

        public static AuthenticationResult SuccessWithDeviceCredentials(string deviceId, string keyValue)
        {
            return new AuthenticationResult
            {
                IsSuccessful = true,
                DeviceId = deviceId,
                Scope = AuthenticationScope.Device,
                KeyValue = keyValue
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

        public string KeyName { get; private set; }
        
        public string KeyValue { get; private set; }

        public AuthenticationScope Scope { get; private set; }
    }
}