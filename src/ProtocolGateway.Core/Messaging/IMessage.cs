// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Messaging
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    public interface IMessage: IDisposable
    {
        Stream Payload { get; }

        string MessageId { get; set; }

        string LockToken { get; }

        IDictionary<string, string> Properties { get; }

        DateTime CreatedTimeUtc { get; }

        uint DeliveryCount { get; }

        ulong SequenceNumber { get; }
    }
}