// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence
{
    using System;
    using DotNetty.Codecs.Mqtt.Packets;

    public interface ISubscription
    {
        DateTime CreationTime { get; }

        string TopicFilter { get; }

        QualityOfService QualityOfService { get; }

        ISubscription CreateUpdated(QualityOfService qos);
    }
}