// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.Gateway.Core.Mqtt
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Configuration;
    using System.Diagnostics.Contracts;
    using System.Linq;

    public sealed class TopicNameRouter : ITopicNameRouter
    {
        const string BaseUriString = "http://x/";
        static readonly Uri BaseUri = new Uri(BaseUriString, UriKind.Absolute);

        UriTemplateTable topicTemplateTable;
        Dictionary<RouteSourceType, UriPathTemplate> routeTemplateMap;

        public TopicNameRouter()
            : this("mqttTopicRouting")
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="TopicNameRouter" /> class.
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
        public TopicNameRouter(string configurationSectionName)
        {
            Contract.Requires(!string.IsNullOrEmpty(configurationSectionName));

            var configuration = (RoutingConfiguration)ConfigurationManager.GetSection(configurationSectionName);
            this.InitializeFromConfiguration(configuration);
        }

        public TopicNameRouter(RoutingConfiguration configuration)
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

        public bool TryMapRouteToTopicName(RouteSourceType routeType, IDictionary<string, string> context, out string topicName)
        {
            UriPathTemplate template;
            if (!this.routeTemplateMap.TryGetValue(routeType, out template))
            {
                topicName = null;
                return false;
            }
            topicName = template.Bind(context);
            return true;
        }

        public bool TryMapTopicNameToRoute(string topicName, out RouteDestinationType routeType, IDictionary<string, string> contextOutput)
        {
            Collection<UriTemplateMatch> matches = this.topicTemplateTable.Match(new Uri(BaseUri, topicName));

            if (matches.Count == 0)
            {
                routeType = RouteDestinationType.Unknown;
                return false;
            }

            if (matches.Count > 1)
            {
                if (BridgeEventSource.Log.IsVerboseEnabled)
                {
                    BridgeEventSource.Log.Verbose("Topic name matches more than one route.", topicName);
                }
            }

            UriTemplateMatch match = matches[0];
            var route = (InboundRouteDefinition)match.Data;
            routeType = route.Type;
            int variableCount = match.BoundVariables.Count;
            for (int i = 0; i < variableCount; i++)
            {
                // todo: this will unconditionally set property values - is it acceptable to overwrite existing value?
                contextOutput[match.BoundVariables.GetKey(i)] = match.BoundVariables.Get(i);
            }
            return true;
        }
    }
}