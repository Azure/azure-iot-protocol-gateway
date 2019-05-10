// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.IotHubClient
{
    using System;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.ProtocolGateway.Instrumentation;
    using Microsoft.Azure.Devices.ProtocolGateway.IotHubClient.Addressing;
    using Microsoft.Azure.Devices.ProtocolGateway.Messaging;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt;

    /// <summary>Provides a way to send messages from client as events to IoT Hub.</summary>
    public class TelemetrySender : IMessagingServiceClient
    {
        readonly IotHubBridge bridge;
        readonly IMessageAddressConverter messageAddressConverter;

        public TelemetrySender(IotHubBridge bridge, IMessageAddressConverter messageAddressConverter)
        {
            this.bridge = bridge;
            this.messageAddressConverter = messageAddressConverter;
        }

        public int MaxPendingMessages => this.bridge.Settings.MaxPendingInboundMessages;

        public IMessage CreateMessage(string address, IByteBuffer payload)
        {
            var message = new IotHubClientMessage(new Message(payload.IsReadable() ? new ReadOnlyByteBufferStream(payload, false) : null), payload);
            message.Address = address;
            return message;
        }

        public void BindMessagingChannel(IMessagingChannel channel)
        {
        }

        public async Task SendAsync(IMessage message)
        {
            var clientMessage = (IotHubClientMessage)message;
            try
            {
                string address = message.Address;
                if (this.messageAddressConverter.TryParseAddressIntoMessageProperties(address, message))
                {
                    string messageDeviceId;
                    if (message.Properties.TryGetValue(TemplateParameters.DeviceIdTemplateParam, out messageDeviceId))
                    {
                        if (!this.bridge.DeviceId.Equals(messageDeviceId, StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException(
                                $"Device ID provided in topic name ({messageDeviceId}) does not match ID of the device publishing message ({this.bridge.DeviceId})");
                        }
                        message.Properties.Remove(TemplateParameters.DeviceIdTemplateParam);
                    }
                }
                else
                {
                    if (!this.bridge.Settings.PassThroughUnmatchedMessages)
                    {
                        throw new InvalidOperationException($"Topic name `{address}` could not be matched against any of the configured routes.");
                    }

                    CommonEventSource.Log.Warning("Topic name could not be matched against any of the configured routes. Falling back to default telemetry settings.", address);
                    message.Properties[this.bridge.Settings.ServicePropertyPrefix + MessagePropertyNames.Unmatched] = bool.TrueString;
                    message.Properties[this.bridge.Settings.ServicePropertyPrefix + MessagePropertyNames.Subject] = address;
                }
                var iotHubMessage = clientMessage.ToMessage();

                await this.bridge.DeviceClient.SendEventAsync(iotHubMessage);
            }
            catch (IotHubException ex)
            {
                throw ComposeIotHubCommunicationException(ex);
            }
            finally
            {
                clientMessage.Dispose();
            }
        }

        public Task AbandonAsync(string messageId) => TaskEx.Completed;

        public Task CompleteAsync(string messageId) => TaskEx.Completed;

        public Task RejectAsync(string messageId) => TaskEx.Completed;

        public Task DisposeAsync(Exception cause) => TaskEx.Completed;

        static MessagingException ComposeIotHubCommunicationException(IotHubException ex)
        {
            return new MessagingException(ex.Message, ex.InnerException, ex.IsTransient, ex.TrackingId);
        }
    }
}