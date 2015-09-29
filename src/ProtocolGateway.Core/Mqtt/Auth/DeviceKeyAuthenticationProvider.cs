// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Auth
{
    using System;
    using System.Net;
    using System.Threading.Tasks;

    public class DeviceKeyAuthenticationProvider : IAuthenticationProvider
    {
        public Task<AuthenticationResult> AuthenticateAsync(string clientId, string username, string password, EndPoint clientAddress)
        {
            if (!clientId.Equals(username, StringComparison.Ordinal))
            {
                return Task.FromResult(AuthenticationResult.Failure());
            }
            return Task.FromResult(AuthenticationResult.SuccessWithDeviceKey(clientId, password));
        }
    }
}