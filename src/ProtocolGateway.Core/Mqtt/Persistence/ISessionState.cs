// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence
{
    using System.Collections.Generic;
    using DotNetty.Codecs.Mqtt.Packets;

    public interface ISessionState
    {
        IReadOnlyList<ISubscription> Subscriptions { get; }

        bool IsTransient { get; }

        ISessionState Copy();

        void AddOrUpdateSubscription(string topicFilter, QualityOfService qos);

        bool RemoveSubscription(string topicFilter);
    }
}