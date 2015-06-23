// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Gateway.Samples.Common
{
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
                        if (buffer.HasArray)
                        {
                            gzipStream.Write(buffer.Array,
                                buffer.ArrayOffset + buffer.ReaderIndex, buffer.ReadableBytes);
                        }
                        else
                        {
                            // todo: optimize
                            while (buffer.IsReadable())
                            {
                                gzipStream.WriteByte(buffer.ReadByte());
                            }
                        }
                    }
                    return new UnpooledHeapByteBuffer(
                        UnpooledByteBufferAllocator.Default, outputStream.ToArray(), (int)outputStream.Length);
                }
            }
            finally
            {
                ReferenceCountUtil.Release(buffer);
            }
        }
    }
}