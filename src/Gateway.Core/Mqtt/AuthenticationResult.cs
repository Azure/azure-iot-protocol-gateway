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

        public static AuthenticationResult Success(string deviceId, AuthenticationScope scope, string token)
        {
            return new AuthenticationResult
            {
                IsSuccessful = true,
                DeviceId = deviceId,
                Scope = scope,
                Token = token
            };
        }

        AuthenticationResult()
        {
        }

        public bool IsSuccessful { get; private set; }

        public string DeviceId { get; private set; }

        public string Token { get; private set; }

        public AuthenticationScope Scope { get; private set; }
    }
}