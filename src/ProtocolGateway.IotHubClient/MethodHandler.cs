// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.IotHubClient
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.IO;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.ProtocolGateway.Instrumentation;
    using Microsoft.Azure.Devices.ProtocolGateway.IotHubClient.Addressing;
    using Microsoft.Azure.Devices.ProtocolGateway.Messaging;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt;

    /// <summary>Dispatches direct method calls as messages to client.</summary>
    public class MethodHandler : IMessagingServiceClient, IMessageDispatcher
    {
        public delegate Task<MethodResponse> MethodHandlerCallback(MethodRequest request, IMessageDispatcher dispatcher);

        readonly DeviceClient deviceClient;
        readonly string deviceId;
        readonly IotHubClientSettings settings;
        IMessagingChannel messagingChannel;
        MethodHandlerCallback dispatchCallback;

        Dictionary<string, TaskCompletionSource<SendMessageOutcome>> messageMap;

        MethodHandler(DeviceClient deviceClient, string deviceId, IotHubClientSettings settings, MethodHandlerCallback dispatchCallback)
        {
            this.deviceClient = deviceClient;
            this.deviceId = deviceId;
            this.settings = settings;
            this.dispatchCallback = dispatchCallback;
        }

        public int MaxPendingMessages => this.settings.MaxPendingInboundMessages;

        public IMessage CreateMessage(string address, IByteBuffer payload)
        {
            throw new InvalidOperationException("Must not receive messages from a client.");
        }

        public async void BindMessagingChannel(IMessagingChannel channel)
        {
            try
            {
                Contract.Requires(channel != null);

                Contract.Assert(this.messagingChannel == null);

                this.messagingChannel = channel;
                await this.deviceClient.SetMethodHandlerAsync(
                    null,
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

        public Task SendAsync(IMessage message)
        {
            return TaskEx.FromException(new InvalidOperationException("Must not receive messages from a client."));
        }

        Task CompleteMessageAsync(string messageId, SendMessageOutcome outcome)
        {
            if (this.messageMap.TryGetValue(messageId, out var promise))
            {
                promise.TrySetResult(outcome);
            }
            return TaskEx.Completed;
        }

        public Task AbandonAsync(string messageId) => this.CompleteMessageAsync(messageId, SendMessageOutcome.Abandonded);

        public Task CompleteAsync(string messageId) => this.CompleteMessageAsync(messageId, SendMessageOutcome.Completed);

        public Task RejectAsync(string messageId) => this.CompleteMessageAsync(messageId, SendMessageOutcome.Rejected);

        public Task DisposeAsync(Exception cause) => TaskEx.Completed;

        Task<SendMessageOutcome> IMessageDispatcher.SendAsync(IMessage message)
        {
            var promise = new TaskCompletionSource<SendMessageOutcome>();
            this.messageMap[message.Id] = promise;
            this.messagingChannel.Handle(this.AttachFeedbackChannel(message));
            return promise.Task;
        }
    }
}