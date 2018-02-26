// <copyright file="ReliableSessionState.cs" company="Microsoft">
//   ReliableSessionState
// </copyright>
// <summary>
//   Defines the ReliableSessionState type.
// </summary>

namespace Microsoft.Azure.Devices.ProtocolGateway.Providers.ReliableStorage
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    using DotNetty.Codecs.Mqtt.Packets;

    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence;

    using Newtonsoft.Json;

    /// <summary>
    /// The reliable session state.
    /// </summary>
    [Serializable]
    [DataContract]
    [KnownType(typeof(List<Subscription>))]
    [KnownType(typeof(List<ISubscription>))]
    [KnownType(typeof(DateTime))]
    [KnownType(typeof(QualityOfService))]
    [KnownType(typeof(Subscription))]
    public class ReliableSessionState : ISessionState
    {
        /// <summary>
        /// The subscriptions.
        /// </summary>
        [IgnoreDataMember]
        [JsonIgnore]
        readonly List<ISubscription> subscriptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReliableSessionState"/> class.
        /// </summary>
        /// <param name="transient">
        /// The transient.
        /// </param>
        /// <param name="compressionTopicIdentifier">
        /// The compression topic identifier.
        /// </param>
        public ReliableSessionState(bool transient)
        {
            this.IsTransient = transient;
            this.subscriptions = new List<ISubscription>();
        }

        /// <summary>
        /// Gets a value indicating whether is transient.
        /// </summary>
        [IgnoreDataMember]
        [JsonIgnore]
        public bool IsTransient { get; }

        /// <summary>
        /// The subscriptions.
        /// </summary>
        [DataMember]
        [JsonProperty]
        public IReadOnlyList<ISubscription> Subscriptions => this.subscriptions;

        /// <summary>
        /// The copy.
        /// </summary>
        /// <returns>
        /// The <see cref="ISessionState"/>.
        /// </returns>
        public ISessionState Copy()
        {
            var sessionState = new ReliableSessionState(this.IsTransient);
            sessionState.subscriptions.AddRange(this.Subscriptions);
            return sessionState;
        }

        /// <summary>
        /// The remove subscription.
        /// </summary>
        /// <param name="topicFilter">
        /// The topic filter.
        /// </param>
        /// <returns>
        /// The <see cref="bool"/>.
        /// </returns>
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

        /// <summary>
        /// The add or update subscription.
        /// </summary>
        /// <param name="topicFilter">
        /// The topic filter.
        /// </param>
        /// <param name="qos">
        /// The QOS.
        /// </param>
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

        /// <summary>
        /// The find subscription index.
        /// </summary>
        /// <param name="topicFilter">
        /// The topic filter.
        /// </param>
        /// <returns>
        /// The <see cref="int"/>.
        /// </returns>
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