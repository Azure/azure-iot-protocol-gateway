// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ProtocolGateway.Host.Common
{
    using System.Diagnostics.Contracts;
    using System.IO;
    using System.IO.Compression;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Mqtt.Packets;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    public class MqttPacketPayloadCompressionHandler : ChannelHandlerAdapter
    {
        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            var packet = message as PublishPacket;
            if (packet != null)
            {
                IByteBuffer result = ApplyCompression(packet.Payload, CompressionMode.Decompress);
                packet.Payload = result;
            }
            context.FireChannelRead(message);
        }

        public override Task WriteAsync(IChannelHandlerContext context, object message)
        {
            var packet = message as PublishPacket;
            if (packet != null)
            {
                IByteBuffer result = ApplyCompression(packet.Payload, CompressionMode.Compress);
                packet.Payload = result;
            }
            return context.WriteAsync(message);
        }

        static IByteBuffer ApplyCompression(IByteBuffer buffer, CompressionMode compressionMode)
        {
            try
            {
                using (var outputStream = new MemoryStream())
                {
                    using (var gzipStream = new GZipStream(outputStream, compressionMode, true))
                    {
                        buffer.ReadBytes(gzipStream, buffer.ReadableBytes);
                    }

                    Contract.Assert(outputStream.Length <= int.MaxValue);

#if NETSTANDARD1_3
                    return Unpooled.WrappedBuffer(outputStream.ToArray());
#else
                    return Unpooled.WrappedBuffer(outputStream.GetBuffer(), 0, (int)outputStream.Length);
#endif
                }
            }
            finally
            {
                ReferenceCountUtil.Release(buffer);
            }
        }
    }
}