// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Messaging
{
    using System;
    using System.Threading.Tasks;
    using DotNetty.Buffers;

    public interface IMessagingServiceClient
    {
        int MaxPendingMessages { get; }

        IMessage CreateMessage(string address, IByteBuffer payload);

        void BindMessagingChannel(IMessagingChannel<IMessage> channel);

        Task SendAsync(IMessage message);

        Task AbandonAsync(string messageId);

        Task CompleteAsync(string messageId);

        Task RejectAsync(string messageId);

        Task DisposeAsync(Exception cause);
    }
}