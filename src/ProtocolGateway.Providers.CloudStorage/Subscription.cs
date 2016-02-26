// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Providers.CloudStorage
{
    using System;
    using System.Diagnostics.Contracts;
    using DotNetty.Codecs.Mqtt.Packets;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence;
    using Newtonsoft.Json;

    public class Subscription : ISubscription
    {
        public Subscription(string topicFilter, QualityOfService qualityOfService)
            : this(DateTime.UtcNow, topicFilter, qualityOfService)
        {
        }

        Subscription(DateTime creationTime, string topicFilter, QualityOfService qualityOfService)
        {
            this.CreationTime = creationTime;
            this.TopicFilter = topicFilter;
            this.QualityOfService = qualityOfService;
        }

        internal Subscription()
        {}


        [JsonProperty]
        public DateTime CreationTime { get; private set; }

        [JsonProperty]
        public string TopicFilter { get; private set; }

        [JsonProperty]
        public QualityOfService QualityOfService { get; private set; }

        [Pure]
        public ISubscription CreateUpdated(QualityOfService qos) => new Subscription(this.CreationTime, this.TopicFilter, qos);
    }
}