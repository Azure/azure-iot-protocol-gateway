// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.IotHubClient
{
    using Microsoft.Azure.Devices.ProtocolGateway.IotHubClient.Mqtt.Routing;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Routing;

    class IotHubMqttCommunicationFactory : IotHubCommunicationFactory, IIotHubMqttCommunicationFactory
    {
        public IotHubMqttCommunicationFactory(IotHubClientFactoryFunc iotHubClientFactoryMethod)
            : base(iotHubClientFactoryMethod)
        {
        }

        public IIotHubMqttMessageRouter CreateMessageRouter()
        {
            return new IotHubMqttMessageRouter();
        }
    }
}