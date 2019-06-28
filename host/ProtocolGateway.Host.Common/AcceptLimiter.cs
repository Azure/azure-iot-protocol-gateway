// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ProtocolGateway.Host.Common
{
    using System.Threading;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// Controls connection accept rate based on number of concurrent connections going through throttled initialization phase
    /// </summary>
    sealed class AcceptLimiter : ChannelHandlerAdapter
    {
        IChannelHandlerContext capturedContext;
        long available;

        public AcceptLimiter(long capacity)
        {
            this.available = capacity;
        }

        public override void ChannelActive(IChannelHandlerContext context)
        {
            this.capturedContext = context;
            base.ChannelActive(context);
            context.Read();
        }

        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            if (Volatile.Read(ref this.available) > 0)
            {
                context.Read();
            }
            base.ChannelRead(context, message);
        }

        public void Claim() => Interlocked.Decrement(ref this.available);

        public void ReleaseClaim()
        {
            Interlocked.Increment(ref this.available);
            this.capturedContext?.Read();
        }
    }
}