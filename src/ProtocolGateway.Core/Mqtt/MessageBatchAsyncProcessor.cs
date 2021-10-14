// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Mqtt
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using DotNetty.Codecs.Mqtt.Packets;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    public class MessageBatchAsyncProcessor : IMessageProcessor<PublishPacket>
    {
        readonly int maxBatchSizeInBytes;
        readonly Func<IChannelHandlerContext, IReadOnlyCollection<PublishPacket>, Task> processFunc;
        readonly Queue<PublishPacket> backlogQueue;
        State state;
        readonly TaskCompletionSource closedPromise;

        public MessageBatchAsyncProcessor(int maxBatchSizeInBytes, Func<IChannelHandlerContext, IReadOnlyCollection<PublishPacket>, Task> processFunc)
        {
            this.maxBatchSizeInBytes = maxBatchSizeInBytes;
            this.processFunc = processFunc;
            this.backlogQueue = new Queue<PublishPacket>();
            this.closedPromise = new TaskCompletionSource();
        }

        public Task Closed => this.closedPromise.Task;

        public int BacklogSize => this.backlogQueue.Count;

        public void Post(IChannelHandlerContext context, PublishPacket packet)
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
                    Queue<PublishPacket> queue = this.backlogQueue;
                    while (queue.Count > 0)
                    {
                        PublishPacket packet = queue.Dequeue();
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
                Queue<PublishPacket> queue = this.backlogQueue;
                while (queue.Count > 0 && this.state != State.Closed)
                {
                    int batchSize = 0;
                    List<PublishPacket> messages = new List<PublishPacket>();
                    do
                    {
                        PublishPacket message = queue.Peek();
                        int messageSize = message.Payload?.ReadableBytes ?? 0 + message.TopicName.Length;
                        if (messageSize + batchSize <= this.maxBatchSizeInBytes)
                        {
                            queue.Dequeue();
                            messages.Add(message);
                        }
                        else
                        {
                            break;
                        }
                    }
                    while (queue.Count > 0);

                    try
                    {
                        await this.ProcessAsync(context, messages);
                        messages.Clear(); // dismissing packet reference as it has been successfully handed off in a form of message
                    }
                    finally
                    {
                        foreach (var message in messages)
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

        Task ProcessAsync(IChannelHandlerContext context, IReadOnlyCollection<PublishPacket> messages)
        {
            return this.processFunc(context, messages);
        }

        enum State
        {
            Idle,
            Processing,
            Closed
        }
    }
}