// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence
{
    using System.Threading.Tasks;
    using DotNetty.Common.Utilities;

    public sealed class TransientSessionStatePersistenceProvider : ISessionStatePersistenceProvider
    {
        public ISessionState Create(bool transient)
        {
            return new TransientSessionState(transient);
        }

        public Task<ISessionState> GetAsync(string id)
        {
            return Task.FromResult((ISessionState)null);
        }

        public Task SetAsync(string id, ISessionState sessionState)
        {
            return TaskEx.Completed;
        }

        public Task DeleteAsync(string id, ISessionState sessionState)
        {
            return TaskEx.Completed;
        }
    }
}