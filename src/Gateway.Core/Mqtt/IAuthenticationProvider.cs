// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.Gateway.Core.Mqtt
{
    using System.Net;
    using System.Threading.Tasks;

    public interface IAuthenticationProvider
    {
        Task<AuthenticationResult> AuthenticateAsync(string clientId, string username, string password, EndPoint clientAddress);
    }
}