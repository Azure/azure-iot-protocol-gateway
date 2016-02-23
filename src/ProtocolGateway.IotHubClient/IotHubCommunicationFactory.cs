// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.IotHubClient
{
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.ProtocolGateway.IotHub;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Auth;

    public class IotHubCommunicationFactory : IIotHubCommunicationFactory
    {
        readonly IotHubClientFactoryFunc iotHubClientFactoryMethod;

        public IotHubCommunicationFactory(IotHubClientFactoryFunc iotHubClientFactoryMethod)
        {
            this.iotHubClientFactoryMethod = iotHubClientFactoryMethod;
        }

        public Task<IIotHubClient> CreateIotHubClientAsync(AuthenticationResult authResult)
        {
            return this.iotHubClientFactoryMethod(authResult);
        }

        public IMessage CreateMessage(Stream bodyStream)
        {
            return new IotHubMessage(new Message(bodyStream));
        }
    }
}