// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Providers.CloudStorage
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.Serialization;
    using DotNetty.Codecs.Mqtt.Packets;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence;

    class BlobSessionState : ISessionState
    {
        [DataMember(Name = "subscriptions")] // todo: name casing seems to be ignored by the serializer
        readonly List<ISubscription> subscriptions;

        public BlobSessionState(bool transient)
        {
            this.IsTransient = transient;
            this.subscriptions = new List<ISubscription>();
        }

        [IgnoreDataMember]
        public bool IsTransient { get; }

        public IReadOnlyList<ISubscription> Subscriptions => this.subscriptions;

        [IgnoreDataMember]
        public string ETag { get; set; }

        public ISessionState Copy()
        {
            var sessionState = new BlobSessionState(this.IsTransient);
            sessionState.ETag = this.ETag;
            sessionState.subscriptions.AddRange(this.Subscriptions);
            return sessionState;
        }

        public bool RemoveSubscription(string topicFilter)
        {
            int index = this.FindSubscriptionIndex(topicFilter);
            if (index >= 0)
            {
                this.subscriptions.RemoveAt(index);
                return true;
            }
            return false;
        }

        public void AddOrUpdateSubscription(string topicFilter, QualityOfService qos)
        {
            int index = this.FindSubscriptionIndex(topicFilter);

            if (index >= 0)
            {
                this.subscriptions[index] = this.subscriptions[index].CreateUpdated(qos);
            }
            else
            {
                this.subscriptions.Add(new Subscription(topicFilter, qos));
            }
        }

        int FindSubscriptionIndex(string topicFilter)
        {
            for (int i = this.subscriptions.Count - 1; i >= 0; i--)
            {
                ISubscription subscription = this.subscriptions[i];
                if (subscription.TopicFilter.Equals(topicFilter, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }
    }
}