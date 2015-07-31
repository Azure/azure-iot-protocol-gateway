// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.Gateway.Core.Mqtt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class InboundRouteDefinition
    {
        InboundRouteDefinition(IEnumerable<string> templates, RouteDestinationType type)
        {
            this.Type = type;
            this.Templates = templates.ToArray();
        }

        public static InboundRouteDefinition Create(IEnumerable<string> templates, RouteDestinationType type)
        {
            var route = new InboundRouteDefinition(templates, type);
            route.Validate();
            return route;
        }

        void Validate()
        {
            if (!this.Templates.Any())
            {
                throw new InvalidOperationException("Route must have at least one template specified.");
            }

            switch (this.Type)
            {
                case RouteDestinationType.Unknown:
                    throw new InvalidOperationException("Route type cannot be `Unknown`.");
                case RouteDestinationType.Telemetry:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public RouteDestinationType Type { get; private set; }

        public IEnumerable<string> Templates { get; private set; }
    }
}