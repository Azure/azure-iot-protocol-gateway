// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Tests.Load
{
    using System;
    using DotNetty.Transport.Channels;

    public class ConsoleLoggingHandler : ChannelHandlerAdapter
    {
        ConsoleLoggingHandler()
        {
        }

        public static ConsoleLoggingHandler Instance { get; } = new ConsoleLoggingHandler();

        public override bool IsSharable => true;

        public override void UserEventTriggered(IChannelHandlerContext context, object evt)
        {
            Console.WriteLine($"User Event: {evt}");
            base.UserEventTriggered(context, evt);
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            Console.WriteLine($"Exception: {exception}");
            base.ExceptionCaught(context, exception);
        }
    }
}