// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.IotHubClient
{
    using System;
    using System.Collections.Generic;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using Microsoft.Azure.Devices.ProtocolGateway.Messaging;
    using Message = Client.Message;

    public sealed class IotHubClientMessage : IMessage
    {
        readonly Message message;

        public IotHubClientMessage(Message message, IByteBuffer payload)
        {
            this.message = message;
            this.Payload = payload;
        }

        public IDictionary<string, string> Properties => this.message.Properties;

        public string Address { get; set; }

        public IByteBuffer Payload { get; }

        public string Id => this.message.LockToken;

        public DateTime CreatedTimeUtc => this.message.EnqueuedTimeUtc;

        public uint DeliveryCount => this.message.DeliveryCount;

        public ulong SequenceNumber => this.message.SequenceNumber;

        public void Dispose()
        {
            this.message.Dispose();
            if (this.Payload != null)
            {
                ReferenceCountUtil.SafeRelease(this.Payload);
            }
        }

        internal Message ToMessage() => this.message;
    }
}