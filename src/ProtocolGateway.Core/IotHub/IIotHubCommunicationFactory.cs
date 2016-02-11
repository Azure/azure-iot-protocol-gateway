// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.IotHub
{
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Auth;

    /// <summary>
    /// Abstract factory of IotHub-related objects
    /// </summary>
    public interface IIotHubCommunicationFactory
    {
        Task<IIotHubClient> CreateIotHubClientAsync(AuthenticationResult authResult);

        IMessage CreateMessage(Stream bodyStream);
    }
}