// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.IotHubClient
{
    using System;
    using System.Diagnostics.Contracts;
    using System.IO;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.ProtocolGateway.IotHubClient.Addressing;
    using Microsoft.Azure.Devices.ProtocolGateway.Messaging;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt;

    public class CommandReceiver : IMessagingServiceClient
    {
        readonly DeviceClient deviceClient;
        readonly string deviceId;
        readonly IotHubClientSettings settings;
        readonly IByteBufferAllocator allocator;
        readonly IMessageAddressConverter messageAddressConverter;
        IMessagingChannel messagingChannel;

        CommandReceiver(DeviceClient deviceClient, string deviceId, IotHubClientSettings settings, IByteBufferAllocator allocator, IMessageAddressConverter messageAddressConverter)
        {
            this.deviceClient = deviceClient;
            this.deviceId = deviceId;
            this.settings = settings;
            this.allocator = allocator;
            this.messageAddressConverter = messageAddressConverter;
        }

        public int MaxPendingMessages => int.MaxValue;

        public IMessage CreateMessage(string address, IByteBuffer payload)
        {
            throw new InvalidOperationException("Must not receive messages from a client.");
        }

        public void BindMessagingChannel(IMessagingChannel channel)
        {
            Contract.Requires(channel != null);

            Contract.Assert(this.messagingChannel == null);

            this.messagingChannel = channel;
            this.Receive();
        }

        public Task SendAsync(IMessage message)
        {
            return TaskEx.FromException(new InvalidOperationException("Must not receive messages from a client."));
        }

        async void Receive()
        {
            Message message = null;
            IByteBuffer messagePayload = null;
            try
            {
                while (true)
                {
                    message = await this.deviceClient.ReceiveAsync(TimeSpan.MaxValue);
                    if (message == null)
                    {
                        this.messagingChannel.Close(null);
                        return;
                    }

                    if (this.settings.MaxOutboundRetransmissionEnforced && message.DeliveryCount > this.settings.MaxOutboundRetransmissionCount)
                    {
                        await this.RejectAsync(message.LockToken);
                        message.Dispose();
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
                    msg.Properties[TemplateParameters.DeviceIdTemplateParam] = this.deviceId;
                    string address;
                    if (!this.messageAddressConverter.TryDeriveAddress(msg, out address))
                    {
                        messagePayload.Release();
                        await this.RejectAsync(message.LockToken); // todo: fork await
                        message.Dispose();
                        continue;
                    }
                    msg.Address = address;

                    this.messagingChannel.Handle(this.AttachFeedbackChannel(msg));

                    message = null; // ownership has been transferred to messagingChannel
                    messagePayload = null;
                }
            }
            catch (IotHubException ex)
            {
                this.messagingChannel.Close(new MessagingException(ex.Message, ex.InnerException, ex.IsTransient, ex.TrackingId));
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

        public async Task AbandonAsync(string messageId)
        {
            try
            {
                await this.deviceClient.AbandonAsync(messageId);
            }
            catch (IotHubException ex)
            {
                throw ComposeIotHubCommunicationException(ex);
            }
        }

        public async Task CompleteAsync(string messageId)
        {
            try
            {
                await this.deviceClient.CompleteAsync(messageId);
            }
            catch (IotHubException ex)
            {
                throw ComposeIotHubCommunicationException(ex);
            }
        }

        public async Task RejectAsync(string messageId)
        {
            try
            {
                await this.deviceClient.RejectAsync(messageId);
            }
            catch (IotHubException ex)
            {
                throw ComposeIotHubCommunicationException(ex);
            }
        }

        public Task DisposeAsync(Exception cause)
        {
            return TaskEx.Completed;
        }

        static MessagingException ComposeIotHubCommunicationException(IotHubException ex)
        {
            return new MessagingException(ex.Message, ex.InnerException, ex.IsTransient, ex.TrackingId);
        }
    }
}