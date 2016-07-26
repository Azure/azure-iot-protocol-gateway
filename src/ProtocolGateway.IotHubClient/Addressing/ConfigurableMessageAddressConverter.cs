// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.IotHubClient.Addressing
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Configuration;
    using System.Diagnostics.Contracts;
    using System.Linq;
    using Microsoft.Azure.Devices.ProtocolGateway.Instrumentation;
    using Microsoft.Azure.Devices.ProtocolGateway.Messaging;

    public sealed class ConfigurableMessageAddressConverter : IMessageAddressConverter
    {
        static readonly Uri BaseUri = new Uri("http://x/", UriKind.Absolute);

        UriTemplateTable topicTemplateTable;
        UriPathTemplate outboundTemplate;

        public ConfigurableMessageAddressConverter()
            : this("mqttTopicNameConversion")
        {
        }

        /// <summary>
        ///     Initializes a new instance of <see cref="ConfigurableMessageAddressConverter" />.
        /// </summary>
        /// <param name="configurationSectionName">Name of configuration section that contains routing configuration.</param>
        /// <remarks>
        ///     This constructor uses a section from application configuration to generate routing configuration.
        /// </remarks>
        /// <example>
        ///     <code>
        ///     <mqttTopicNameConversion>
        ///             <inboundTemplate>{deviceId}/messages/events</inboundTemplate>
        ///             <outboundTemplate>devices/{deviceId}/messages/devicebound/{*subTopic}</outboundTemplate>
        ///         </mqttTopicNameConversion>
        /// </code>
        /// </example>
        public ConfigurableMessageAddressConverter(string configurationSectionName)
        {
            Contract.Requires(!string.IsNullOrEmpty(configurationSectionName));

            var configuration = (MessageAddressConversionConfiguration)ConfigurationManager.GetSection(configurationSectionName);
            this.InitializeFromConfiguration(configuration);
        }

        public ConfigurableMessageAddressConverter(MessageAddressConversionConfiguration configuration)
        {
            this.InitializeFromConfiguration(configuration);
        }

        public ConfigurableMessageAddressConverter(List<string> inboundTemplates, List<string> outboundTemplates)
        {
            var configuration = new MessageAddressConversionConfiguration(inboundTemplates, outboundTemplates);
            this.InitializeFromConfiguration(configuration);
        }

        void InitializeFromConfiguration(MessageAddressConversionConfiguration configuration)
        {
            Contract.Requires(configuration != null);

            this.topicTemplateTable = new UriTemplateTable(
                BaseUri,
                from template in configuration.InboundTemplates select new KeyValuePair<UriTemplate, object>(new UriTemplate(template, false), null));
            this.topicTemplateTable.MakeReadOnly(true);
            this.outboundTemplate = configuration.OutboundTemplates.Select(x => new UriPathTemplate(x)).Single();
        }

        public bool TryDeriveAddress(IMessage message, out string address)
        {
            UriPathTemplate template = this.outboundTemplate;
            try
            {
                address = template.Bind(message.Properties);
            }
            catch (InvalidOperationException)
            {
                address = null;
                return false;
            }
            return true;
        }

        public bool TryParseAddressIntoMessageProperties(string address, IMessage message)
        {
            Collection<UriTemplateMatch> matches = this.topicTemplateTable.Match(new Uri(BaseUri, address));

            if (matches.Count == 0)
            {
                return false;
            }

            if (matches.Count > 1)
            {
                if (CommonEventSource.Log.IsVerboseEnabled)
                {
                    CommonEventSource.Log.Verbose("Topic name matches more than one route.", address);
                }
            }

            UriTemplateMatch match = matches[0];
            int variableCount = match.BoundVariables.Count;
            for (int i = 0; i < variableCount; i++)
            {
                // todo: this will unconditionally set property values - is it acceptable to overwrite existing value?
                message.Properties.Add(match.BoundVariables.GetKey(i), match.BoundVariables.Get(i));
            }
            return true;
        }
    }
}