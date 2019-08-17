// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.IotHubClient
{
    using System;
    using System.Diagnostics.Contracts;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.ProtocolGateway.Instrumentation;
    using Microsoft.Azure.Devices.ProtocolGateway.Messaging;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt;
    using Message = Client.Message;

    public class CommandReceiver : IMessagingSource
    {
        public delegate bool TryFormatAddress(IMessage message, out string address);

        readonly IotHubBridge bridge;
        readonly IByteBufferAllocator allocator;
        readonly TryFormatAddress addressFormatter;
        IMessagingChannel messagingChannel;
        CancellationTokenSource lifetimeCancellation;

        public CommandReceiver(IotHubBridge bridge, IByteBufferAllocator allocator, TryFormatAddress addressFormatter)
        {
            this.bridge = bridge;
            this.allocator = allocator;
            this.addressFormatter = addressFormatter;
        }

        public int MaxPendingMessages => int.MaxValue;

        public IMessage CreateMessage(string address, IByteBuffer payload) => throw new InvalidOperationException("Must not receive messages from a client.");

        public void BindMessagingChannel(IMessagingChannel channel)
        {
            Contract.Requires(channel != null);

            Contract.Assert(this.messagingChannel == null);

            this.messagingChannel = channel;
            this.lifetimeCancellation = new CancellationTokenSource();
            this.Receive();
        }

        async void Receive()
        {
            Message message = null;
            IByteBuffer messagePayload = null;
            try
            {
                while (true)
                {
                    message = await this.bridge.DeviceClient.ReceiveAsync(this.lifetimeCancellation.Token);
                    if (message == null)
                    {
                        this.messagingChannel.Close(null);
                        return;
                    }

                    PerformanceCounters.TotalCommandsReceived.Increment();
                    PerformanceCounters.CommandsReceivedPerSecond.Increment();

                    if (this.bridge.Settings.MaxOutboundRetransmissionEnforced && message.DeliveryCount > this.bridge.Settings.MaxOutboundRetransmissionCount)
                    {
                        CommonEventSource.Log.Info("Rejected message: high delivery count: " + message.DeliveryCount, null, this.bridge.DeviceId);
                        await this.RejectAsync(message.LockToken);
                        message.Dispose();
                        message = null;
                        continue;
                    }

                    using (Stream payloadStream = message.GetBodyStream())
                    {
                        long streamLength = payloadStream.Length;
                        if (streamLength > int.MaxValue)
                        {
                            throw new InvalidOperationException($"Message size ({streamLength.ToString()} bytes) is too big to process.");
                        }

                        int length = (int)streamLength;
                        messagePayload = this.allocator.Buffer(length, length);
                        await messagePayload.WriteBytesAsync(payloadStream, length);

                        Contract.Assert(messagePayload.ReadableBytes == length);
                    }

                    var msg = new IotHubClientMessage(message, messagePayload);
                    msg.Properties[TemplateParameters.DeviceIdTemplateParam] = this.bridge.DeviceId;
                    string address;
                    if (!this.addressFormatter(msg, out address))
                    {
                        CommonEventSource.Log.Info("Rejected message: could not format topic", null, this.bridge.DeviceId);
                        messagePayload.Release();
                        await this.RejectAsync(message.LockToken); // todo: fork await
                        message.Dispose();
                        message = null;
                        messagePayload = null;
                        continue;
                    }
                    msg.Address = address;

                    this.messagingChannel.Handle(msg, this);

                    message = null; // ownership has been transferred to messagingChannel
                    messagePayload = null;
                }
            }
            catch (IotHubException ex)
            {
                this.messagingChannel.Close(ex.ToMessagingException());
            }
            catch (Exception ex)
            {
                this.messagingChannel.Close(ex);
            }
            finally
            {
                message?.Dispose();
                if (messagePayload != null)
                {
                    ReferenceCountUtil.SafeRelease(messagePayload);
                }
            }
        }

        public async Task CompleteAsync(string messageId)
        {
            try
            {
                await this.bridge.DeviceClient.CompleteAsync(messageId);
            }
            catch (IotHubException ex)
            {
                throw ex.ToMessagingException();
            }
        }

        public async Task RejectAsync(string messageId)
        {
            try
            {
                await this.bridge.DeviceClient.RejectAsync(messageId);
            }
            catch (IotHubException ex)
            {
                throw ex.ToMessagingException();
            }
        }

        public Task DisposeAsync(Exception cause)
        {
            this.lifetimeCancellation?.Cancel();
            return TaskEx.Completed;
        }
    }
}