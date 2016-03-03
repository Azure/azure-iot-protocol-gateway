// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence
{
    using System;
    using System.Diagnostics.Contracts;
    using DotNetty.Codecs.Mqtt.Packets;

    public class TransientSubscription : ISubscription
    {
        public TransientSubscription(string topicFilter, QualityOfService qualityOfService)
            : this(DateTime.UtcNow, topicFilter, qualityOfService)
        {
        }

        TransientSubscription(DateTime creationTime, string topicFilter, QualityOfService qualityOfService)
        {
            this.CreationTime = creationTime;
            this.TopicFilter = topicFilter;
            this.QualityOfService = qualityOfService;
        }

        public DateTime CreationTime { get; }

        public string TopicFilter { get; }

        public QualityOfService QualityOfService { get; }

        [Pure]
        public ISubscription CreateUpdated(QualityOfService qos) => new TransientSubscription(this.CreationTime, this.TopicFilter, qos);
    }
}