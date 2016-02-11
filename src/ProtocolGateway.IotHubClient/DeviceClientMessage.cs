// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.IotHubClient
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.ProtocolGateway.IotHub;

    public sealed class DeviceClientMessage : IMessage
    {
        private readonly Message message;

        public DeviceClientMessage(Message message)
        {
            this.message = message;
        }

        public IDictionary<string, string> Properties => this.message.Properties;

        public Stream Body => this.message.GetBodyStream();

        public string LockToken => this.message.LockToken;

        public DateTime CreatedTimeUtc => this.message.EnqueuedTimeUtc;

        public uint DeliveryCount => this.message.DeliveryCount;

        public ushort SequenceNumber => unchecked ((ushort)this.message.SequenceNumber);

        public string MessageId
        {
            get { return this.message.MessageId; }
            set { this.message.MessageId = value; }
        }

        public void Dispose()
        {
            this.message.Dispose();
        }

        internal Message ToMessage()
        {
            return this.message;
        }
    }
}