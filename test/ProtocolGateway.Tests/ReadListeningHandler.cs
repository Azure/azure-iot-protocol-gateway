// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using DotNetty.Transport.Channels;

    public sealed class ReadListeningHandler : ChannelHandlerAdapter
    {
        readonly Queue<object> receivedQueue = new Queue<object>();
        readonly ConcurrentQueue<TaskCompletionSource<object>> readPromises;
        Exception registeredException;
        readonly TimeSpan defaultReadTimeout;

        public ReadListeningHandler()
            : this(TimeSpan.Zero)
        {
        }

        public ReadListeningHandler(TimeSpan defaultReadTimeout)
        {
            this.defaultReadTimeout = defaultReadTimeout;
            this.readPromises = new ConcurrentQueue<TaskCompletionSource<object>>();
        }

        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            TaskCompletionSource<object> promise;
            if (this.readPromises.TryDequeue(out promise))
            {
                promise.TrySetResult(message);
            }
            else
            {
                this.receivedQueue.Enqueue(message);
            }
        }

        public override void ChannelInactive(IChannelHandlerContext context)
        {
            this.SetException(new InvalidOperationException("Channel is closed."));
            base.ChannelInactive(context);
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception) => this.SetException(exception);

        void SetException(Exception exception)
        {
            this.registeredException = exception;
            TaskCompletionSource<object> promise;
            while (this.readPromises.TryDequeue(out promise))
            {
                promise.TrySetException(exception);
            }
        }

        public async Task<object> ReceiveAsync(TimeSpan timeout = default(TimeSpan))
        {
            if (this.registeredException != null)
            {
                throw this.registeredException;
            }

            if (this.receivedQueue.Count > 0)
            {
                return this.receivedQueue.Dequeue();
            }

            var promise = new TaskCompletionSource<object>();
            this.readPromises.Enqueue(promise);

            timeout = timeout <= TimeSpan.Zero ? this.defaultReadTimeout : timeout;
            if (timeout > TimeSpan.Zero)
            {
                Task task = await Task.WhenAny(promise.Task, Task.Delay(timeout));
                if (task != promise.Task)
                {
                    throw new TimeoutException("ReceiveAsync timed out");
                }

                return promise.Task.Result;
            }

            return await promise.Task;
        }
    }
}