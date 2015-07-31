// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.Gateway.Core.Mqtt
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    public sealed class AsyncChannelMessageProcessor<T>
    {
        readonly Queue<T> queue;
        State state;
        readonly Func<IChannelHandlerContext, T, Task> processFunc;
        readonly TaskCompletionSource completionSource;

        public AsyncChannelMessageProcessor(Func<IChannelHandlerContext, T, Task> processFunc)
        {
            this.queue = new Queue<T>();
            this.completionSource = new TaskCompletionSource();
            this.processFunc = processFunc;
        }

        public Task Completion
        {
            get { return this.completionSource.Task; }
        }

        public int Count
        {
            get { return this.queue.Count; }
        }

        public void Post(IChannelHandlerContext context, T message)
        {
            switch (this.state)
            {
                case State.Idle:
                    this.queue.Enqueue(message);
                    this.state = State.Processing;
                    this.StartQueueProcessingAsync(context);
                    break;
                case State.Processing:
                case State.FinalProcessing:
                    this.queue.Enqueue(message);
                    break;
                case State.Aborted:
                    ReferenceCountUtil.Release(message);
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

                    Queue<T> q = this.queue;
                    if (q != null)
                    {
                        while (q.Count > 0)
                        {
                            T packet = q.Dequeue();
                            ReferenceCountUtil.Release(packet);
                        }
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
                while (this.queue.Count > 0 && this.state != State.Aborted)
                {
                    T message = this.queue.Dequeue();
                    try
                    {
                        await this.processFunc(context, message);
                    }
                    finally
                    {
                        ReferenceCountUtil.Release(message);
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

        enum State
        {
            Idle,
            Processing,
            FinalProcessing,
            Aborted
        }
    }
}