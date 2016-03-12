// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence
{
    using System;

    public interface IQos2MessageDeliveryState
    {
        DateTime LastModified { get; }

        ulong SequenceNumber { get; }
    }
}