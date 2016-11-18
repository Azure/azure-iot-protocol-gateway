// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Mqtt
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    public abstract class MessageAsyncProcessorBase<T>
    {
        readonly string scope;
        readonly Queue<T> backlogQueue;
        State state;
        readonly TaskCompletionSource completionSource;

        protected MessageAsyncProcessorBase(string scope)
        {
            this.scope = scope;
            this.backlogQueue = new Queue<T>();
            this.completionSource = new TaskCompletionSource();
        }

        public Task Completion => this.completionSource.Task;

        public int BacklogSize => this.backlogQueue.Count;

        public void Post(IChannelHandlerContext context, T packet)
        {
            switch (this.state)
            {
                case State.Idle:
                    this.backlogQueue.Enqueue(packet);
                    this.state = State.Processing;
                    this.StartQueueProcessingAsync(context);
                    break;
                case State.Processing:
                case State.FinalProcessing:
                    this.backlogQueue.Enqueue(packet);
                    break;
                case State.Aborted:
                    ReferenceCountUtil.Release(packet);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void Complete()
        {
            switch (this.state)
            {
                case State.Idle:
                    this.completionSource.TryComplete();
                    break;
                case State.Processing:
                    this.state = State.FinalProcessing;
                    break;
                case State.FinalProcessing:
                case State.Aborted:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void Abort()
        {
            switch (this.state)
            {
                case State.Idle:
                case State.Processing:
                case State.FinalProcessing:
                    this.state = State.Aborted;

                    Queue<T> queue = this.backlogQueue;
                    while (queue.Count > 0)
                    {
                        T packet = queue.Dequeue();
                        ReferenceCountUtil.SafeRelease(packet);
                    }
                    break;
                case State.Aborted:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        async void StartQueueProcessingAsync(IChannelHandlerContext context)
        {
            try
            {
                Queue<T> queue = this.backlogQueue;
                while (queue.Count > 0 && this.state != State.Aborted)
                {
                    T message = queue.Dequeue();
                    try
                    {
                        await this.ProcessAsync(context, message, this.scope);
                        message = default(T); // dismissing packet reference as it has been successfully handed off in a form of message
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
                    case State.FinalProcessing:
                    case State.Aborted:
                        this.completionSource.TryComplete();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            catch (Exception ex)
            {
                this.Abort();
                this.completionSource.TrySetException(new ChannelMessageProcessingException(ex, context));
            }
        }

        protected abstract Task ProcessAsync(IChannelHandlerContext context, T packet, string scope);

        enum State
        {
            Idle,
            Processing,
            FinalProcessing,
            Aborted
        }
    }
}