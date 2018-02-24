// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Subscription.cs" company="Microsoft">
//   Subscription
// </copyright>
// <summary>
//   Defines the Subscription type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Microsoft.Azure.Devices.ProtocolGateway.Providers.ReliableStorage
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Runtime.Serialization;

    using DotNetty.Codecs.Mqtt.Packets;

    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence;

    using Newtonsoft.Json;

    /// <summary>
    /// The subscription.
    /// </summary>
    [Serializable]
    [DataContract]
    public class Subscription : ISubscription
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Subscription"/> class.
        /// </summary>
        /// <param name="topicFilter">
        /// The topic filter.
        /// </param>
        /// <param name="qualityOfService">
        /// The quality of service.
        /// </param>
        public Subscription(string topicFilter, QualityOfService qualityOfService)
            : this(DateTime.UtcNow, topicFilter, qualityOfService)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Subscription"/> class.
        /// </summary>
        internal Subscription()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Subscription"/> class.
        /// </summary>
        /// <param name="creationTime">
        /// The creation time.
        /// </param>
        /// <param name="topicFilter">
        /// The topic filter.
        /// </param>
        /// <param name="qualityOfService">
        /// The quality of service.
        /// </param>
        Subscription(DateTime creationTime, string topicFilter, QualityOfService qualityOfService)
        {
            this.CreationTime = creationTime;
            this.TopicFilter = topicFilter;
            this.QualityOfService = qualityOfService;
        }

        /// <summary>
        /// Gets the creation time.
        /// </summary>
        [JsonProperty]
        [DataMember]
        public DateTime CreationTime { get; private set; }

        /// <summary>
        /// Gets the topic filter.
        /// </summary>
        [JsonProperty]
        [DataMember]
        public string TopicFilter { get; private set; }

        /// <summary>
        /// Gets the quality of service.
        /// </summary>
        [DataMember]
        [JsonProperty]
        public QualityOfService QualityOfService { get; private set; }

        /// <summary>
        /// The create updated.
        /// </summary>
        /// <param name="qos">
        /// The QOS.
        /// </param>
        /// <returns>
        /// The <see cref="ISubscription"/>.
        /// </returns>
        [Pure]
        public ISubscription CreateUpdated(QualityOfService qos) => new Subscription(this.CreationTime, this.TopicFilter, qos);
    }
}