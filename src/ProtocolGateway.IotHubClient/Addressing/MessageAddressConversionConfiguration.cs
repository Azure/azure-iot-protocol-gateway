// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.IotHubClient.Addressing
{
    using System.Collections.Generic;

    public sealed class MessageAddressConversionConfiguration
    {
        public MessageAddressConversionConfiguration()
        {
            this.InboundTemplates = new List<string>();
            this.OutboundTemplates = new List<string>();
        }

        internal MessageAddressConversionConfiguration(List<string> inboundTemplates, List<string> outboundTemplates)
        {
            this.InboundTemplates = inboundTemplates;
            this.OutboundTemplates = outboundTemplates;
        }

        public List<string> InboundTemplates { get; private set; }

        public List<string> OutboundTemplates { get; private set; }
    }
}