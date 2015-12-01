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
            Identity identity = Identity.Parse(username);
            if (!clientId.Equals(identity.DeviceId, StringComparison.Ordinal))
            {
                return Task.FromResult((AuthenticationResult)null);
            }
            return Task.FromResult(AuthenticationResult.SuccessWithDeviceKey(identity, password));
        }
    }
}