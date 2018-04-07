// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Providers.ReliableStorage
{
    using System;
    using System.Runtime.Serialization;

    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence;
    using Newtonsoft.Json;

    [Serializable]
    [DataContract]
    [KnownType(typeof(DateTime))]
    [KnownType(typeof(long))]
    [KnownType(typeof(ulong))]
    public class ReliableMessageDeliveryState : IQos2MessageDeliveryState
    {
        public ReliableMessageDeliveryState()
        {
        }

        public ReliableMessageDeliveryState(ulong sequenceNumber)
        {
            this.MessageNumber = unchecked ((long)sequenceNumber);
            this.LastModified = DateTime.UtcNow;
        }

        [DataMember]
        [JsonProperty]
        public DateTime LastModified { get; set; }

        [DataMember]
        [JsonProperty]
        public long MessageNumber { get; set; }

        [IgnoreDataMember]
        [JsonIgnore]
        ulong IQos2MessageDeliveryState.SequenceNumber => unchecked((ulong)this.MessageNumber);
    }
}