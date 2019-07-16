// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Mqtt
{
    using System;
    using System.Threading.Tasks;
    using DotNetty.Transport.Channels;

    public sealed class MessageAsyncProcessor<T> : MessageAsyncProcessorBase<T>
    {
        readonly Func<IChannelHandlerContext, T, Task> processFunc;

        public MessageAsyncProcessor(Func<IChannelHandlerContext, T, Task> processFunc)
        {
            this.processFunc = processFunc;
        }

        protected override Task ProcessAsync(IChannelHandlerContext context, T packet) => this.processFunc(context, packet);
    }
}