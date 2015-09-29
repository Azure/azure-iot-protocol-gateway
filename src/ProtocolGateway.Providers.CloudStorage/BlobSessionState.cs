// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Providers.CloudStorage
{
    using System.Collections.Generic;
    using System.Runtime.Serialization;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence;

    class BlobSessionState : ISessionState
    {
        public BlobSessionState(bool transient)
        {
            this.IsTransient = transient;
            this.Subscriptions = new List<Subscription>();
        }

        [IgnoreDataMember]
        public bool IsTransient { get; private set; }

        [DataMember(Name = "subscriptions")] // todo: name casing seems to be ignored by the serializer
        public List<Subscription> Subscriptions { get; private set; } // todo: own subscription type for serialization

        [IgnoreDataMember]
        public string ETag { get; set; }

        public ISessionState Copy()
        {
            var sessionState = new BlobSessionState(this.IsTransient);
            sessionState.ETag = this.ETag;
            sessionState.Subscriptions.AddRange(this.Subscriptions);
            return sessionState;
        }
    }
}