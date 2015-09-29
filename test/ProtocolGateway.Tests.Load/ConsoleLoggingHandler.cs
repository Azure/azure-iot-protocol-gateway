// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Gateway.Tests.Load
{
    using System;
    using DotNetty.Transport.Channels;

    public class ConsoleLoggingHandler : ChannelHandlerAdapter
    {
        static readonly ConsoleLoggingHandler instance = new ConsoleLoggingHandler();

        ConsoleLoggingHandler()
        {
        }

        public static ConsoleLoggingHandler Instance
        {
            get { return instance; }
        }

        public override bool IsSharable
        {
            get { return true; }
        }

        public override void UserEventTriggered(IChannelHandlerContext context, object evt)
        {
            Console.WriteLine("User Event: {0}", evt);
            base.UserEventTriggered(context, evt);
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            Console.WriteLine("Exception: {0}", exception);
            base.ExceptionCaught(context, exception);
        }
    }
}