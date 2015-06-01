// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.Gateway.Core.Mqtt
{
    using System.Threading.Tasks;

    public class StubAuthenticationProvider : IAuthenticationProvider
    {
        public Task<AuthenticationResult> AuthenticateAsync(string clientId, string username, string password)
        {
            return Task.FromResult(AuthenticationResult.Success(clientId, AuthenticationScope.None, null));
        }
    }
}