// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence
{
    using System.Security.Principal;
    using System.Threading.Tasks;

    public interface ISessionStatePersistenceProvider
    {
        ISessionState Create(bool transient);

        Task<ISessionState> GetAsync(IIdentity identity);

        Task SetAsync(IIdentity identity, ISessionState sessionState);

        Task DeleteAsync(IIdentity identity, ISessionState sessionState);
    }
}