// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Messaging
{
    using System;
    using System.Threading.Tasks;
    using DotNetty.Buffers;

    public interface IMessagingServiceClient
    {
        IMessage CreateMessage(string address, IByteBuffer payload);

        Task SendAsync(IMessage message);

        Task DisposeAsync(Exception cause);
    }
}