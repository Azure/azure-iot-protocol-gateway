// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ProtocolGateway.IotHubClient.Routing
{
    using System;
    using System.Configuration;
    using System.Linq;
    using System.Xml;
    using Microsoft.Azure.Devices.ProtocolGateway.IotHub.Routing;

    public class TopicRoutingConfigurationHandler : IConfigurationSectionHandler
    {
        public object Create(object parent, object configContext, XmlNode section)
        {
            var configuration = new RoutingConfiguration();
            configuration.InboundRoutes.AddRange(section
                .OfType<XmlElement>()
                .Where(x => "inboundRoute".Equals(x.Name, StringComparison.OrdinalIgnoreCase))
                .Select(route => InboundRouteDefinition.Create(
                    route.OfType<XmlElement>().Where(x => "template".Equals(x.Name, StringComparison.OrdinalIgnoreCase)).Select(x => x.InnerText),
                    ParseInboundRouteType(route.GetAttribute("to")))));
            configuration.OutboundRoutes.AddRange(section
                .OfType<XmlElement>()
                .Where(x => "outboundRoute".Equals(x.Name, StringComparison.OrdinalIgnoreCase))
                .Select(route => OutboundRouteDefinition.Create(
                    ParseOutboundRouteType(route.GetAttribute("from")),
                    route.OfType<XmlElement>().Single(x => "template".Equals(x.Name, StringComparison.OrdinalIgnoreCase)).InnerText)));
            return configuration;
        }

        static RouteDestinationType ParseInboundRouteType(string value)
        {
            RouteDestinationType result;
            if (Enum.TryParse(value, true, out result))
            {
                return result;
            }
            throw new ConfigurationErrorsException(string.Format("routeType value `{0}`could not be parsed.", value));
        }

        static RouteSourceType ParseOutboundRouteType(string value)
        {
            RouteSourceType result;
            if (Enum.TryParse(value, true, out result))
            {
                return result;
            }
            throw new ConfigurationErrorsException(string.Format("routeType value `{0}`could not be parsed.", value));
        }
    }
}