// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ProtocolGateway.Host.Common
{
    using DotNetty.Handlers.Tls;
    using DotNetty.Transport.Channels;

    sealed class AcceptLimiterTlsReleaseHandler : ChannelHandlerAdapter
    {
        readonly AcceptLimiter limiter;
        bool claimReturned;

        public AcceptLimiterTlsReleaseHandler(AcceptLimiter limiter)
        {
            this.limiter = limiter;
        }

        public override void ChannelActive(IChannelHandlerContext context)
        {
            this.limiter.Claim();
            base.ChannelActive(context);
        }

        public override void ChannelInactive(IChannelHandlerContext context)
        {
            this.ReleaseClaim();
            base.ChannelInactive(context);
        }

        public override void UserEventTriggered(IChannelHandlerContext context, object evt)
        {
            if (evt is TlsHandshakeCompletionEvent tls) // TLS handshake is done so we return claim
            {
                this.ReleaseClaim();
                context.Channel.Pipeline.Remove(this);
            }
            base.UserEventTriggered(context, evt);
        }

        void ReleaseClaim()
        {
            if (!this.claimReturned)
            {
                this.claimReturned = true;
                this.limiter.ReleaseClaim();
            }
        }
    }
}