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
        readonly Queue<T> backlogQueue;
        State state;
        readonly TaskCompletionSource closedPromise;

        protected MessageAsyncProcessorBase()
        {
            this.backlogQueue = new Queue<T>();
            this.closedPromise = new TaskCompletionSource();
        }

        public Task Closed => this.closedPromise.Task;

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
                    Queue<T> queue = this.backlogQueue;
                    while (queue.Count > 0)
                    {
                        T packet = queue.Dequeue();
                        ReferenceCountUtil.SafeRelease(packet);
                    }
                    break;
                case State.Closed:
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
                while (queue.Count > 0 && this.state != State.Closed)
                {
                    T message = queue.Dequeue();
                    try
                    {
                        await this.ProcessAsync(context, message);
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
                    case State.Closed:
                        this.closedPromise.TryComplete();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            catch (Exception ex)
            {
                this.closedPromise.TrySetException(new ChannelMessageProcessingException(ex, context));
                this.Close();
            }
        }

        protected abstract Task ProcessAsync(IChannelHandlerContext context, T packet);

        enum State
        {
            Idle,
            Processing,
            Closed
        }
    }
}