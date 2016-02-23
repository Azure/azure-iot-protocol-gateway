// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Mqtt
{
    using Microsoft.Azure.Devices.ProtocolGateway.IotHub;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Routing;

    /// <summary>
    /// Abstract factory of IotHub-related objects
    /// </summary>
    public interface IIotHubMqttCommunicationFactory : IIotHubCommunicationFactory
    {
        IIotHubMqttMessageRouter CreateMessageRouter();
    }
}