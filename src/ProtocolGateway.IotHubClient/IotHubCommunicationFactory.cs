// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.IotHubClient
{
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.ProtocolGateway.Identity;
    using Microsoft.Azure.Devices.ProtocolGateway.Messaging;

    public class IotHubCommunicationFactory : IMessagingFactory
    {
        readonly IotHubClientFactoryFunc iotHubClientFactoryMethod;

        public IotHubCommunicationFactory(IotHubClientFactoryFunc iotHubClientFactoryMethod)
        {
            this.iotHubClientFactoryMethod = iotHubClientFactoryMethod;
        }

        public Task<IMessagingServiceClient> CreateIotHubClientAsync(IDeviceIdentity authResult)
        {
            return this.iotHubClientFactoryMethod(authResult);
        }

        public IMessage CreateMessage(Stream payload)
        {
            return new DeviceClientMessage(new Message(payload));
        }
    }
}