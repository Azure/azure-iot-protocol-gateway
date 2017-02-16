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
        where TAckState : ISupportRetransmission, IPacketReference
    {
        // ReSharper disable once StaticMemberInGenericType -- generic type is used sparingly
        static readonly Action<object, object> StartRetransmissionIfNeededCallback = StartRetransmissionIfNeeded;

        Queue<TAckState> pendingAckQueue;
        readonly Func<IChannelHandlerContext, TAckState, Task> processAckFunc;
        readonly Action<IChannelHandlerContext, TAckState> triggerRetransmissionAction;
        bool retransmissionCheckScheduled;
        readonly TimeSpan ackTimeout;
        readonly bool abortOnOutOfOrderAck;

        public RequestAckPairProcessor(Func<IChannelHandlerContext, TAckState, Task> processAckFunc,
            Action<IChannelHandlerContext, TAckState> triggerRetransmissionAction, TimeSpan? ackTimeout, bool abortOnOutOfOrderAck, string scope)
            : base(scope)
        {
            Contract.Requires(!ackTimeout.HasValue || ackTimeout.Value > TimeSpan.Zero);

            this.processAckFunc = processAckFunc;
            this.triggerRetransmissionAction = triggerRetransmissionAction;
            this.ackTimeout = ackTimeout ?? TimeSpan.Zero;
            this.abortOnOutOfOrderAck = abortOnOutOfOrderAck;
        }

        public TAckState FirstRequestPendingAck => this.RequestPendingAckCount == 0 ? default(TAckState) : this.pendingAckQueue.Peek();

        public int RequestPendingAckCount => this.pendingAckQueue?.Count ?? 0;

        public bool Retransmitting { get; set; }

        Queue<TAckState> PendingAckQueue => this.pendingAckQueue ?? (this.pendingAckQueue = new Queue<TAckState>(4));

        bool AckCanTimeout => this.ackTimeout > TimeSpan.Zero;

        public Task SendRequestAsync(IChannelHandlerContext context, TRequest requestMessage, TAckState ackState)
        {
            this.PendingAckQueue.Enqueue(ackState);

            if (this.CheckAndScheduleRetransmission(context))
            {
                // retransmission is underway so the message has to be sent as part of retransmission;
                // we cannot abandon message right now as it would mess up the order so we leave message in the queue without sending it.
                return TaskEx.Completed;
            }

            return Util.WriteMessageAsync(context, requestMessage);
        }

        public async Task RetransmitAsync(IChannelHandlerContext context, TRequest message, TAckState state)
        {
            state.ResetSentTime();
            await context.WriteAndFlushAsync(message);
            this.ScheduleRetransmissionCheck(context, this.ackTimeout);
        }

        public bool ResumeRetransmission(IChannelHandlerContext context)
        {
            if (this.Retransmitting)
            {
                this.triggerRetransmissionAction(context, this.FirstRequestPendingAck);
                return true;
            }

            return false;
        }

        /// <summary>
        /// </summary>
        /// <param name="context"></param>
        /// <returns>true if retransmission is already underway, otherwise false.</returns>
        bool CheckAndScheduleRetransmission(IChannelHandlerContext context)
        {
            if (this.Retransmitting)
            {
                return true;
            }

            if (this.AckCanTimeout)
            {
                // if retransmission is configured, schedule check for timeout when posting first message in queue
                this.ScheduleRetransmissionCheck(context, this.ackTimeout);
            }
            return false;
        }

        void ScheduleRetransmissionCheck(IChannelHandlerContext context, TimeSpan delay)
        {
            Contract.Requires(this.AckCanTimeout);

            if (!this.retransmissionCheckScheduled)
            {
                this.retransmissionCheckScheduled = true;
                context.Channel.EventLoop.ScheduleAsync(StartRetransmissionIfNeededCallback, context, this, delay);
            }
        }

        static void StartRetransmissionIfNeeded(object ctx, object state)
        {
            var self = (RequestAckPairProcessor<TAckState, TRequest>)state;

            self.retransmissionCheckScheduled = false;

            TAckState messageState = self.FirstRequestPendingAck;
            if (messageState != null)
            {
                TimeSpan timeoutLeft = self.ackTimeout - (DateTime.UtcNow - messageState.SentTime);
                var context = (IChannelHandlerContext)ctx;
                if (timeoutLeft.Ticks <= 0)
                {
                    // entering retransmission mode
                    self.Retransmitting = true;
                    self.triggerRetransmissionAction(context, messageState);
                }
                else
                {
                    // rescheduling check for when timeout would happen for current top message pending ack
                    self.ScheduleRetransmissionCheck(context, timeoutLeft);
                }
            }
        }

        bool TryDequeueMessage(PacketWithId packet, out TAckState message, string scope)
        {
            TAckState firstRequest = this.FirstRequestPendingAck;
            if (firstRequest == null)
            {
                CommonEventSource.Log.Warning($"{packet.PacketType.ToString()} #{packet.PacketId.ToString()} was received while not expected.", scope);
                message = default(TAckState);
                return false;
            }

            if (packet.PacketId != firstRequest.PacketId)
            {
                CommonEventSource.Log.Warning($"{packet.PacketType.ToString()} #{packet.PacketId.ToString()} was received while #{firstRequest.PacketId.ToString()} was expected.", scope);
                message = default(TAckState);
                return false;
            }

            TAckState dequeued = this.pendingAckQueue.Dequeue();

            Contract.Assert(ReferenceEquals(dequeued, firstRequest));

            if (this.pendingAckQueue.Count == 0)
            {
                this.Retransmitting = false;
            }

            message = firstRequest;
            return true;
        }

        protected override Task ProcessAsync(IChannelHandlerContext context, PacketWithId packet, string scope)
        {
            TAckState message;
            if (this.TryDequeueMessage(packet, out message, scope))
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