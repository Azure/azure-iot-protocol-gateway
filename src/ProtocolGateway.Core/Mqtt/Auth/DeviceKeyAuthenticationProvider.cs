// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Auth
{
    using System;
    using System.Net;
    using System.Threading.Tasks;

    public class DeviceKeyAuthenticationProvider : IAuthenticationProvider
    {
        /// <summary>
        /// Authenticates user
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="clientAddress"></param>
        /// <returns></returns>
        public Task<AuthenticationResult> AuthenticateAsync(string clientId, string username, string password, EndPoint clientAddress)
        {
            if (!clientId.Equals(username, StringComparison.Ordinal))
            {
                return Task.FromResult((AuthenticationResult)null);
            }
            return Task.FromResult(AuthenticationResult.SuccessWithDeviceKey(new SimpleIdentity(username, true), password));
        }
    }
}