// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Mqtt
{
    using System;
    using System.Threading.Tasks;
    using DotNetty.Transport.Channels;

    public sealed class PacketAsyncProcessor<T> : PacketAsyncProcessorBase<T>
    {
        readonly Func<IChannelHandlerContext, T, Task> processFunc;

        public PacketAsyncProcessor(Func<IChannelHandlerContext, T, Task> processFunc)
        {
            this.processFunc = processFunc;
        }

        protected override Task ProcessAsync(IChannelHandlerContext context, T packet)
        {
            return this.processFunc(context, packet);
        }
    }
}