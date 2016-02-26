// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence
{
    using System.Threading.Tasks;
    using DotNetty.Common.Utilities;
    using Microsoft.Azure.Devices.ProtocolGateway.Identity;

    public sealed class TransientSessionStatePersistenceProvider : ISessionStatePersistenceProvider
    {
        public ISessionState Create(bool transient)
        {
            return new TransientSessionState(transient);
        }

        public Task<ISessionState> GetAsync(IDeviceIdentity identity)
        {
            return Task.FromResult((ISessionState)null);
        }

        public Task SetAsync(IDeviceIdentity identity, ISessionState sessionState)
        {
            return TaskEx.Completed;
        }

        public Task DeleteAsync(IDeviceIdentity identity, ISessionState sessionState)
        {
            return TaskEx.Completed;
        }
    }
}