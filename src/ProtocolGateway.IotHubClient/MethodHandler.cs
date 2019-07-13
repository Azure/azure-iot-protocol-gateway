// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.IotHubClient
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.ProtocolGateway.Messaging;

    /// <summary>Dispatches direct method calls as messages to client.</summary>
    public class MethodHandler : IMessagingSource, IMessageDispatcher
    {
        public delegate Task<MethodResponse> MethodHandlerCallback(MethodRequest request, IMessageDispatcher dispatcher);

        readonly string methodName;
        readonly IotHubBridge bridge;
        readonly MethodHandlerCallback dispatchCallback;
        IMessagingChannel messagingChannel;
        Dictionary<string, TaskCompletionSource<SendMessageOutcome>> messageMap;

        public MethodHandler(string methodName, IotHubBridge bridge, MethodHandlerCallback dispatchCallback)
        {
            this.methodName = methodName;
            this.bridge = bridge;
            this.dispatchCallback = dispatchCallback;
        }

        public int MaxPendingMessages => this.bridge.Settings.MaxPendingInboundMessages;

        public Dictionary<string, TaskCompletionSource<SendMessageOutcome>> MessageMap => this.messageMap ?? (this.messageMap = new Dictionary<string, TaskCompletionSource<SendMessageOutcome>>(3));

        public IMessage CreateMessage(string address, IByteBuffer payload) => throw new InvalidOperationException("Must not receive messages from a client.");

        public async void BindMessagingChannel(IMessagingChannel channel)
        {
            try
            {
                Contract.Requires(channel != null);

                Contract.Assert(this.messagingChannel == null);

                this.messagingChannel = channel;
                await this.bridge.DeviceClient.SetMethodHandlerAsync(
                    this.methodName,
                    (req, self) =>
                    {
                        var handler = (MethodHandler)self;
                        return handler.dispatchCallback(req, handler);
                    },
                    this);
            }
            catch (Exception ex)
            {
                this.messagingChannel.Close(ex);
            }
        }

        Task CompleteMessageAsync(string messageId, SendMessageOutcome outcome)
        {
            if (this.MessageMap.TryGetValue(messageId, out var promise))
            {
                promise.TrySetResult(outcome);
            }
            return TaskEx.Completed;
        }

        public Task CompleteAsync(string messageId) => this.CompleteMessageAsync(messageId, SendMessageOutcome.Completed);

        public Task RejectAsync(string messageId) => this.CompleteMessageAsync(messageId, SendMessageOutcome.Rejected);

        public Task DisposeAsync(Exception cause) => TaskEx.Completed;

        Task<SendMessageOutcome> IMessageDispatcher.SendAsync(IMessage message)
        {
            var promise = new TaskCompletionSource<SendMessageOutcome>();
            this.MessageMap[message.Id] = promise;
            this.messagingChannel.Handle(message, this);
            return promise.Task;
        }
    }
}