// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.IotHub
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    public interface IMessage: IDisposable
    {
        Stream Body { get; }

        string MessageId { get; set; }

        string LockToken { get; }

        IDictionary<string, string> Properties { get; }

        DateTime CreatedTimeUtc { get; }

        uint DeliveryCount { get; }

        /// <remarks>
        /// Only 15 least significant bits will be honored
        /// </remarks>
        ushort SequenceNumber { get; }
    }
}