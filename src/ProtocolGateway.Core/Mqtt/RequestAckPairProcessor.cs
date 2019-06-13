// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Mqtt
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Threading.Tasks;
    using DotNetty.Codecs.Mqtt.Packets;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using Microsoft.Azure.Devices.ProtocolGateway.Instrumentation;

    sealed class RequestAckPairProcessor<TAckState, TRequest> : MessageAsyncProcessorBase<PacketWithId>
        where TAckState : IPacketReference
    {
        Queue<TAckState> pendingAckQueue;
        readonly Func<IChannelHandlerContext, TAckState, Task> processAckFunc;
        readonly bool abortOnOutOfOrderAck;
        readonly IConnectionIdentityProvider identityProvider;

        public RequestAckPairProcessor(Func<IChannelHandlerContext, TAckState, Task> processAckFunc, bool abortOnOutOfOrderAck, IConnectionIdentityProvider identityProvider)
        {
            this.processAckFunc = processAckFunc;
            this.abortOnOutOfOrderAck = abortOnOutOfOrderAck;
            this.identityProvider = identityProvider;
        }

        public TAckState FirstRequestPendingAck => this.RequestPendingAckCount == 0 ? default(TAckState) : this.pendingAckQueue.Peek();

        public int RequestPendingAckCount => this.pendingAckQueue?.Count ?? 0;

        Queue<TAckState> PendingAckQueue => this.pendingAckQueue ?? (this.pendingAckQueue = new Queue<TAckState>(4));

        public Task SendRequestAsync(IChannelHandlerContext context, TRequest requestMessage, TAckState ackState)
        {
            this.PendingAckQueue.Enqueue(ackState);
            return Util.WriteMessageAsync(context, requestMessage);
        }

        bool TryDequeueMessage(PacketWithId packet, out TAckState message)
        {
            TAckState firstRequest = this.FirstRequestPendingAck;
            if (firstRequest == null)
            {
                CommonEventSource.Log.Warning($"{packet.PacketType.ToString()} #{packet.PacketId.ToString()} was received while not expected.", this.identityProvider.ChannelId, this.identityProvider.Id);
                message = default(TAckState);
                return false;
            }

            if (packet.PacketId != firstRequest.PacketId)
            {
                CommonEventSource.Log.Warning($"{packet.PacketType.ToString()} #{packet.PacketId.ToString()} was received while #{firstRequest.PacketId.ToString()} was expected.", this.identityProvider.ChannelId, this.identityProvider.Id);
                message = default(TAckState);
                return false;
            }

            TAckState dequeued = this.pendingAckQueue.Dequeue();

            Contract.Assert(ReferenceEquals(dequeued, firstRequest));

            message = firstRequest;
            return true;
        }

        protected override Task ProcessAsync(IChannelHandlerContext context, PacketWithId packet)
        {
            TAckState message;
            if (this.TryDequeueMessage(packet, out message))
            {
                return this.processAckFunc(context, message);
            }
            else if (this.abortOnOutOfOrderAck)
            {
                return TaskEx.FromException(new ProtocolGatewayException(ErrorCode.InvalidPubAckOrder, "Client MUST send PUBACK packets in the order in which the corresponding PUBLISH packets were received"));
            }

            return TaskEx.Completed;
        }
    }
}