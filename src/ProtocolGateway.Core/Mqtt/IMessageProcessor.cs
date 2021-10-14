// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Mqtt
{
    using System.Threading.Tasks;
    using DotNetty.Transport.Channels;

    public interface IMessageProcessor<T>
    {
        void Post(IChannelHandlerContext context, T message);

        int BacklogSize { get; }

        Task Closed { get; }

        void Close();
    }
}