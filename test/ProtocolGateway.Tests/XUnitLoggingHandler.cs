namespace Microsoft.Azure.Devices.Gateway.Tests
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
            this.output.WriteLine(string.Format("User Event: {0}", evt));
            base.UserEventTriggered(context, evt);
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            this.output.WriteLine(string.Format("Exception: {0}", exception));
            base.ExceptionCaught(context, exception);
        }
    }
}