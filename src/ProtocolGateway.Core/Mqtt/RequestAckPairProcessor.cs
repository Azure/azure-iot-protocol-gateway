// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Mqtt
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Threading.Tasks;
    using DotNetty.Codecs.Mqtt.Packets;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using Microsoft.Azure.Devices.ProtocolGateway.Instrumentation;

    sealed class RequestAckPairProcessor<TAckState, TRequest>
        where TAckState : IPacketReference
    {
        enum State
        {
            Idle,
            Processing,
            Closed
        }

        readonly Queue<PacketWithId> backlogQueue;
        State state;
        readonly TaskCompletionSource closedPromise;
        readonly IChannelHandlerContext channelContext;
        readonly Func<TAckState, Task> processAckFunc;
        readonly IConnectionIdentityProvider identityProvider;
        readonly bool abortOnOutOfOrderAck;
        Queue<TAckState> pendingAckQueue;

        public RequestAckPairProcessor(IChannelHandlerContext channelContext, Func<TAckState, Task> processAckFunc, bool abortOnOutOfOrderAck, IConnectionIdentityProvider identityProvider)
        {
            this.backlogQueue = new Queue<PacketWithId>();
            this.closedPromise = new TaskCompletionSource();
            this.channelContext = channelContext;
            this.processAckFunc = processAckFunc;
            this.abortOnOutOfOrderAck = abortOnOutOfOrderAck;
            this.identityProvider = identityProvider;
        }

        public Task Closed => this.closedPromise.Task;

        public int BacklogSize => this.backlogQueue.Count;

        TAckState FirstRequestPendingAck => this.RequestPendingAckCount == 0 ? default(TAckState) : this.pendingAckQueue.Peek();

        int RequestPendingAckCount => this.pendingAckQueue?.Count ?? 0;

        Queue<TAckState> PendingAckQueue => this.pendingAckQueue ?? (this.pendingAckQueue = new Queue<TAckState>(4));

        public Task SendRequestAsync(TRequest requestMessage, TAckState ackState)
        {
            this.PendingAckQueue.Enqueue(ackState);
            return Util.WriteMessageAsync(this.channelContext, requestMessage);
        }

        public void Post(PacketWithId packet)
        {
            switch (this.state)
            {
                case State.Idle:
                    this.backlogQueue.Enqueue(packet);
                    this.state = State.Processing;
                    this.StartQueueProcessingAsync();
                    break;
                case State.Processing:
                    this.backlogQueue.Enqueue(packet);
                    break;
                case State.Closed:
                    ReferenceCountUtil.Release(packet);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void Close()
        {
            switch (this.state)
            {
                case State.Idle:
                    this.state = State.Closed;
                    this.closedPromise.TryComplete();
                    break;
                case State.Processing:
                    this.state = State.Closed;
                    Queue<PacketWithId> queue = this.backlogQueue;
                    while (queue.Count > 0)
                    {
                        PacketWithId packet = queue.Dequeue();
                        ReferenceCountUtil.SafeRelease(packet);
                    }
                    break;
                case State.Closed:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        async void StartQueueProcessingAsync()
        {
            try
            {
                Queue<PacketWithId> queue = this.backlogQueue;
                while (queue.Count > 0 && this.state != State.Closed)
                {
                    PacketWithId message = queue.Dequeue();
                    try
                    {
                        await this.ProcessAsync(message);
                        message = default(PacketWithId); // dismissing packet reference as it has been successfully handed off in a form of message
                    }
                    finally
                    {
                        if (message != null)
                        {
                            ReferenceCountUtil.SafeRelease(message);
                        }
                    }
                }

                switch (this.state)
                {
                    case State.Processing:
                        this.state = State.Idle;
                        break;
                    case State.Closed:
                        this.closedPromise.TryComplete();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            catch (Exception ex)
            {
                this.closedPromise.TrySetException(new ChannelMessageProcessingException(ex, this.channelContext));
                this.Close();
            }
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

        Task ProcessAsync(PacketWithId packet)
        {
            TAckState message;
            if (this.TryDequeueMessage(packet, out message))
            {
                return this.processAckFunc(message);
            }
            else if (this.abortOnOutOfOrderAck)
            {
                return TaskEx.FromException(new ProtocolGatewayException(ErrorCode.InvalidPubAckOrder, "Client MUST send PUBACK packets in the order in which the corresponding PUBLISH packets were received"));
            }

            return TaskEx.Completed;
        }
    }
}