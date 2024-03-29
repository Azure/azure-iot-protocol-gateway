// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence
{
    using System;
    using System.Collections.Generic;
    using DotNetty.Codecs.Mqtt.Packets;

    class TransientSessionState : ISessionState
    {
        readonly List<ISubscription> subscriptions;

        public TransientSessionState(bool transient)
        {
            this.IsTransient = transient;
            this.subscriptions = new List<ISubscription>();
        }

        public bool IsTransient { get; }

        public IReadOnlyList<ISubscription> Subscriptions => this.subscriptions;

        public ISessionState Copy()
        {
            var sessionState = new TransientSessionState(this.IsTransient);
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

        public bool AddOrUpdateSubscription(string topicFilter, QualityOfService qos)
        {
            int index = this.FindSubscriptionIndex(topicFilter);

            if (index >= 0)
            {
                if (this.subscriptions[index].QualityOfService != qos)
                {
                    this.subscriptions[index] = this.subscriptions[index].CreateUpdated(qos);
                    return true;
                }
            }
            else
            {
                this.subscriptions.Add(new TransientSubscription(topicFilter, qos));
                return true;
            }
            return false;
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