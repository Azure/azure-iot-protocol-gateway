// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Mqtt
{
    using DotNetty.Common;

    public class TypedPacket<T> : IReferenceCounted where T : IReferenceCounted
    {
        public T Packet { get; set; }
        
        public string Type { get; set; }

        public TypedPacket(T packet, string type)
        {
            this.Packet = packet;
            this.Type = type;
        }

        IReferenceCounted IReferenceCounted.Retain()
        {
            this.Packet.Retain();
            return this;
        }

        IReferenceCounted IReferenceCounted.Retain(int increment)
        {
            this.Packet.Retain(increment);
            return this;
        }

        IReferenceCounted IReferenceCounted.Touch()
        {
            this.Packet.Touch();
            return this;
        }

        IReferenceCounted IReferenceCounted.Touch(object hint)
        {
            this.Packet.Touch(hint);
            return this;
        }

        bool IReferenceCounted.Release()
        {
            return this.Packet.Release();
        }

        bool IReferenceCounted.Release(int decrement)
        {
            return this.Packet.Release(decrement);
        }

        int IReferenceCounted.ReferenceCount { get { return this.Packet.ReferenceCount; } }
    }
}