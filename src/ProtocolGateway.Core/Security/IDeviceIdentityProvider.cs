// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Security
{
    using System.Net;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;

    public interface IDeviceIdentityProvider
    {
        Task<IDeviceIdentity> AuthenticateAsync(string clientId, string username, string password, EndPoint clientAddress);

        Task<IDeviceIdentity> AuthenticateAsync(string clientId, X509Certificate remoteCertificate);
    }
}