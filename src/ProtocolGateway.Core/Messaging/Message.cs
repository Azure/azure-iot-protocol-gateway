// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Messaging
{
    using System;
    using System.Collections.Generic;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;

    public sealed class Message : IMessage
    {
        Dictionary<string, string> properties;

        public string Address { get; set; }

        public IByteBuffer Payload { get; set; }

        public string Id { get; set; }

        public IDictionary<string, string> Properties => this.properties ?? (this.properties = new Dictionary<string, string>(3));

        public DateTime CreatedTimeUtc { get; set; }

        public uint DeliveryCount { get; set; }

        public ulong SequenceNumber { get; set; }

        public void Dispose()
        {
            if (this.Payload != null)
            {
                ReferenceCountUtil.SafeRelease(this.Payload);
            }
        }
    }
}