// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Routing
{
    using System.Collections.Generic;

    public sealed class RoutingConfiguration
    {
        public RoutingConfiguration()
        {
            this.InboundRoutes = new List<InboundRouteDefinition>();
            this.OutboundRoutes = new List<OutboundRouteDefinition>();
        }

        public List<InboundRouteDefinition> InboundRoutes { get; private set; }

        public List<OutboundRouteDefinition> OutboundRoutes { get; private set; }
    }
}