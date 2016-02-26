// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.ProtocolGateway.Identity;

    public interface ISessionStatePersistenceProvider
    {
        ISessionState Create(bool transient);

        Task<ISessionState> GetAsync(IDeviceIdentity identity);

        Task SetAsync(IDeviceIdentity identity, ISessionState sessionState);

        Task DeleteAsync(IDeviceIdentity identity, ISessionState sessionState);
    }
}