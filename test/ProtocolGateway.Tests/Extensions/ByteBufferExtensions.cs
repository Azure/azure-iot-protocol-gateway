namespace Microsoft.Azure.Devices.ProtocolGateway.Tests.Extensions
{
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using System;

    public static class ByteBufferExtensions
    {
        public static byte[] ToArray(this IByteBuffer buffer)
        {
            if (!buffer.IsReadable())
            {
                return ArrayExtensions.ZeroBytes;
            }
            var segment = buffer.GetIoBuffer();
            var result = new byte[segment.Count];
            Array.Copy(segment.Array, segment.Offset, result, 0, segment.Count);
            return result;
        }
    }
}
