// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Messaging
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IBatchAwareMessagingServiceClient : IMessagingServiceClient
    {
        Task SendBatchAsync(IReadOnlyCollection<IMessage> messages);

        int MaxBatchSize { get; }
    }
}