// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.IotHubClient
{
    using System;
    using System.Collections.Generic;
    using DotNetty.Buffers;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.ProtocolGateway.Messaging;

    public sealed class IotHubClientMessage : IMessage
    {
        readonly Message message;

        public IotHubClientMessage(Message message, IByteBuffer payload)
        {
            this.message = message;
            this.Payload = payload;
        }

        public IotHubClientMessage(string address, Message message)
            : this(message, null)
        {
            this.Address = address;
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
            this.message.BodyStream?.Dispose();
        }

        internal Message ToMessage()
        {
            return this.message;
        }
    }
}