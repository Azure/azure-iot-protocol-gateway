// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.Gateway.Core.Mqtt
{
    using System.Threading.Tasks;

    public interface ISessionStateManager
    {
        ISessionState Create(bool transient);

        Task<ISessionState> GetAsync(string id);

        Task SetAsync(string id, ISessionState sessionState);

        Task DeleteAsync(string id, ISessionState sessionState);
    }
}