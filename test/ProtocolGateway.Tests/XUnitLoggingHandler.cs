// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Tests
{
    using System;
    using DotNetty.Transport.Channels;
    using Xunit.Abstractions;

    public class XUnitLoggingHandler : ChannelHandlerAdapter
    {
        readonly ITestOutputHelper output;

        public XUnitLoggingHandler(ITestOutputHelper output)
        {
            this.output = output;
        }

        public override void UserEventTriggered(IChannelHandlerContext context, object evt)
        {
            this.output.WriteLine($"User Event: {evt}");
            base.UserEventTriggered(context, evt);
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            this.output.WriteLine($"Exception: {exception}");
            base.ExceptionCaught(context, exception);
        }
    }
}