// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.IotHubClient.Routing
{
    using System;
    using Microsoft.Azure.Devices.ProtocolGateway.IotHub.Routing;

    public class OutboundRouteDefinition
    {
        OutboundRouteDefinition(RouteSourceType type, string template)
        {
            this.Type = type;
            this.Template = template;
        }

        public static OutboundRouteDefinition Create(RouteSourceType type, string template)
        {
            var route = new OutboundRouteDefinition(type, template);
            route.Validate();
            return route;
        }

        void Validate()
        {
            switch (this.Type)
            {
                case RouteSourceType.Unknown:
                    throw new InvalidOperationException("Route type cannot be `Unknown`.");
                case RouteSourceType.Notification:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public RouteSourceType Type { get; private set; }

        public string Template { get; private set; }
    }
}