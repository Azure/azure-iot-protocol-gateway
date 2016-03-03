// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.IotHubClient
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.ProtocolGateway.Identity;

    public sealed class SasTokenDeviceIdentityProvider : IDeviceIdentityProvider
    {
        public Task<IDeviceIdentity> GetAsync(string clientId, string username, string password, EndPoint clientAddress)
        {
            IotHubDeviceIdentity deviceIdentity;
            if (!IotHubDeviceIdentity.TryParse(username, out deviceIdentity) || !clientId.Equals(deviceIdentity.Id, StringComparison.Ordinal))
            {
                return Task.FromResult(UnauthenticatedDeviceIdentity.Instance);
            }
            deviceIdentity.WithSasToken(password);
            return Task.FromResult<IDeviceIdentity>(deviceIdentity);
        }
    }
}