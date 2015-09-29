// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Mqtt
{
    using System;

    interface ISupportRetransmission
    {
        DateTime SentTime { get; }

        void ResetSentTime();
    }
}