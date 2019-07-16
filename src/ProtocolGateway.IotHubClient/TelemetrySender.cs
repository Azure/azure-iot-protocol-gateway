// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.IotHubClient
{
    using System;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.ProtocolGateway.Instrumentation;
    using Microsoft.Azure.Devices.ProtocolGateway.Messaging;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt;
    using Message = Client.Message;

    /// <summary>Provides a way to send messages from client as events to IoT Hub.</summary>
    public class TelemetrySender : IMessagingServiceClient
    {
        public delegate bool TryProcessMessage(IMessage message);

        readonly IotHubBridge bridge;
        readonly TryProcessMessage messageProcessor;

        public TelemetrySender(IotHubBridge bridge, TryProcessMessage messageProcessor)
        {
            this.bridge = bridge;
            this.messageProcessor = messageProcessor;
        }

        public int MaxPendingMessages => this.bridge.Settings.MaxPendingInboundMessages;

        public IMessage CreateMessage(string address, IByteBuffer payload)
        {
            var message = new IotHubClientMessage(new Message(payload.IsReadable() ? new ReadOnlyByteBufferStream(payload, false) : null), payload);
            message.Address = address;
            return message;
        }

        public async Task SendAsync(IMessage message)
        {
            var clientMessage = (IotHubClientMessage)message;
            try
            {
                if (this.messageProcessor(message))
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
                        throw new InvalidOperationException($"Topic name `{message.Address}` could not be matched against any of the configured routes.");
                    }

                    CommonEventSource.Log.Warning("Topic name could not be matched against any of the configured routes. Falling back to default telemetry settings: " + message.Address, null, this.bridge.DeviceId);
                    message.Properties[this.bridge.Settings.ServicePropertyPrefix + MessagePropertyNames.Unmatched] = bool.TrueString;
                    message.Properties[this.bridge.Settings.ServicePropertyPrefix + MessagePropertyNames.Subject] = message.Address;
                }
                var iotHubMessage = clientMessage.ToMessage();

                await this.bridge.DeviceClient.SendEventAsync(iotHubMessage);
            }
            catch (IotHubException ex)
            {
                throw ex.ToMessagingException();
            }
            finally
            {
                clientMessage.Dispose();
            }
        }

        public Task DisposeAsync(Exception cause) => TaskEx.Completed;
    }
}