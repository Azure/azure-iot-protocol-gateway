namespace Microsoft.Azure.Devices.ProtocolGateway.Mqtt
{
    using DotNetty.Buffers;
    using DotNetty.Common;

    public class FeedbackPacket : IReferenceCounted
    {
        public IByteBuffer Payload { get; set; }

        IReferenceCounted IReferenceCounted.Retain()
        {
            this.Payload.Retain();
            return this;
        }

        IReferenceCounted IReferenceCounted.Retain(int increment)
        {
            this.Payload.Retain(increment);
            return this;
        }

        IReferenceCounted IReferenceCounted.Touch()
        {
            this.Payload.Touch();
            return this;
        }

        IReferenceCounted IReferenceCounted.Touch(object hint)
        {
            this.Payload.Touch(hint);
            return this;
        }

        bool IReferenceCounted.Release()
        {
            return this.Payload.Release();
        }

        bool IReferenceCounted.Release(int decrement)
        {
            return this.Payload.Release(decrement);
        }

        int IReferenceCounted.ReferenceCount { get { return this.Payload.ReferenceCount; } }
    }
}