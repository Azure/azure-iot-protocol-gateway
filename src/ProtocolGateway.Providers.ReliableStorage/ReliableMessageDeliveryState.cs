// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Providers.ReliableStorage
{
    using System;
    using System.Runtime.Serialization;

    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence;


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

        public DateTime LastModified { get; }

        public long MessageNumber { get; }

        ulong IQos2MessageDeliveryState.SequenceNumber => unchecked((ulong)this.MessageNumber);
    }
}