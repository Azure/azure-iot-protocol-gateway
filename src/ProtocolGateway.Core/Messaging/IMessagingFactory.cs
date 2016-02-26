// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Messaging
{
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.ProtocolGateway.Identity;

    /// <summary>
    /// Abstract factory of IotHub-related objects
    /// </summary>
    public interface IMessagingFactory
    {
        Task<IMessagingServiceClient> CreateIotHubClientAsync(IDeviceIdentity authResult);

        IMessage CreateMessage(Stream payload);
    }
}