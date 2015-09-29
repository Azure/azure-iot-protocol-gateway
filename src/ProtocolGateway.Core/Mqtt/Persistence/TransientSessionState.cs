// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence
{
    using System.Collections.Generic;

    class TransientSessionState : ISessionState
    {
        public TransientSessionState(bool transient)
        {
            this.IsTransient = transient;
            this.Subscriptions = new List<Subscription>();
        }

        public bool IsTransient { get; private set; }

        public List<Subscription> Subscriptions { get; private set; }

        public ISessionState Copy()
        {
            var sessionState = new TransientSessionState(this.IsTransient);
            sessionState.Subscriptions.AddRange(this.Subscriptions);
            return sessionState;
        }
    }
}