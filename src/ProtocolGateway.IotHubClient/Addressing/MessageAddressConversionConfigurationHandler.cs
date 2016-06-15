// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.IotHubClient.Addressing
{
    using System;
    using System.Configuration;
    using System.Linq;
    using System.Xml;

    public class MessageAddressConversionConfigurationHandler : IConfigurationSectionHandler
    {
        public object Create(object parent, object configContext, XmlNode section)
        {
            var configuration = new MessageAddressConversionConfiguration();
            configuration.InboundTemplates.AddRange(section
                .OfType<XmlElement>()
                .Where(x => "inboundTemplate".Equals(x.Name, StringComparison.OrdinalIgnoreCase))
                .Select(route => route.InnerText));
            configuration.OutboundTemplates.AddRange(section
                .OfType<XmlElement>()
                .Where(x => "outboundTemplate".Equals(x.Name, StringComparison.OrdinalIgnoreCase))
                .Select(route => route.InnerText));
            return configuration;
        }
    }
}