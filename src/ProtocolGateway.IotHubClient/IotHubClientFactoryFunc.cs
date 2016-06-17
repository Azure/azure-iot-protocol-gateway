// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.IotHubClient
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.ProtocolGateway.Messaging;
    using Microsoft.Azure.Devices.ProtocolGateway.Security;

    public delegate Task<IMessagingServiceClient> IotHubClientFactoryFunc(IDeviceIdentity deviceIdentity);
}