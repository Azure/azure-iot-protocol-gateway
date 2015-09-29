// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Routing
{
    using System.Collections.Generic;

    public interface ITopicNameRouter
    {
        bool TryMapRouteToTopicName(RouteSourceType routeType, IDictionary<string, string> context, out string topicName);

        bool TryMapTopicNameToRoute(string topicName, out RouteDestinationType routeType, IDictionary<string, string> contextOutput);
    }
}