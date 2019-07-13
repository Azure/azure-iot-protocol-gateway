// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Messaging
{
    using System;
    using System.Threading.Tasks;

    public interface IMessagingSource
    {
        void BindMessagingChannel(IMessagingChannel channel);

        Task CompleteAsync(string messageId);

        Task RejectAsync(string messageId);

        Task DisposeAsync(Exception cause);
    }
}