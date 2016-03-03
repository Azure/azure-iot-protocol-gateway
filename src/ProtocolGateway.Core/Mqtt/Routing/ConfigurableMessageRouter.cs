// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Configuration;
    using System.Diagnostics.Contracts;
    using System.Linq;
    using Microsoft.Azure.Devices.ProtocolGateway;
    using Microsoft.Azure.Devices.ProtocolGateway.Instrumentation;
    using Microsoft.Azure.Devices.ProtocolGateway.Messaging;
    using Microsoft.Azure.Devices.ProtocolGateway.Routing;

    public sealed class ConfigurableMessageRouter : IMessageRouter
    {
        const string BaseUriString = "http://x/";
        static readonly Uri BaseUri = new Uri(BaseUriString, UriKind.Absolute);

        UriTemplateTable topicTemplateTable;
        Dictionary<RouteSourceType, UriPathTemplate> routeTemplateMap;
        public ConfigurableMessageRouter()
            : this("mqttTopicRouting")
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="ConfigurableMessageRouter" /> class.
        /// </summary>
        /// <param name="configurationSectionName">Name of configuration section that contains routing configuration.</param>
        /// <remarks>
        ///     This constructor uses a section from application configuration to generate routing configuration.
        /// </remarks>
        /// <example>
        ///     <mqttTopicRouting>
        ///         <inboundRoute to="telemetry">
        ///             <template>{deviceId}/messages/events</template>
        ///             <!-- ... -->
        ///         </inboundRoute>
        ///         <outboundRoute from="notification">
        ///             <template>devices/{deviceId}/messages/devicebound/{*subTopic}</template>
        ///         </outboundRoute>
        ///     </mqttTopicRouting>
        /// </example>
        public ConfigurableMessageRouter(string configurationSectionName)
        {
            Contract.Requires(!string.IsNullOrEmpty(configurationSectionName));

            var configuration = (RoutingConfiguration)ConfigurationManager.GetSection(configurationSectionName);
            this.InitializeFromConfiguration(configuration);
        }

        public ConfigurableMessageRouter(RoutingConfiguration configuration)
        {
            this.InitializeFromConfiguration(configuration);
        }

        void InitializeFromConfiguration(RoutingConfiguration configuration)
        {
            Contract.Requires(configuration != null);

            this.topicTemplateTable = new UriTemplateTable(
                BaseUri,
                (from route in configuration.InboundRoutes
                    from template in route.Templates
                    select new KeyValuePair<UriTemplate, object>(new UriTemplate(template, false), route)));
            this.topicTemplateTable.MakeReadOnly(true);
            this.routeTemplateMap = configuration.OutboundRoutes.ToDictionary(x => x.Type, x => new UriPathTemplate(x.Template));
        }

        /// <summary>
        /// Tries to route a device bound message and append route by message metadata
        /// </summary>
        /// <param name="routeType"></param>
        /// <param name="context"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        public bool TryRouteOutgoingMessage(RouteSourceType routeType, IMessage context, out string path)
        {
            UriPathTemplate template;
            if (!this.routeTemplateMap.TryGetValue(routeType, out template))
            {
                path = null;
                return false;
            }
            try
            {
                path = template.Bind(context.Properties);
            }
            catch (InvalidOperationException)
            {
                path = null;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Tries to route the message to the destination and appends route properties to message metadata
        /// </summary>
        /// <param name="path"></param>
        /// <param name="message"></param>
        /// <param name="routeType"></param>
        /// <returns></returns>
        public bool TryRouteIncomingMessage(string path, IMessage message, out RouteDestinationType routeType)
        {
            Collection<UriTemplateMatch> matches = this.topicTemplateTable.Match(new Uri(BaseUri, path));

            if (matches.Count == 0)
            {
                routeType = RouteDestinationType.Unknown;
                return false;
            }

            if (matches.Count > 1)
            {
                if (MqttIotHubAdapterEventSource.Log.IsVerboseEnabled)
                {
                    MqttIotHubAdapterEventSource.Log.Verbose("Topic name matches more than one route.", path);
                }
            }

            UriTemplateMatch match = matches[0];
            var route = (InboundRouteDefinition)match.Data;
            routeType = route.Type;
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