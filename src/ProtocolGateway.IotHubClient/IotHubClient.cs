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
    using Microsoft.Azure.Devices.ProtocolGateway.Identity;
    using Microsoft.Azure.Devices.ProtocolGateway.Instrumentation;
    using Microsoft.Azure.Devices.ProtocolGateway.IotHubClient.Addressing;
    using Microsoft.Azure.Devices.ProtocolGateway.Messaging;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt;

    public class IotHubClient : IMessagingServiceClient
    {
        readonly DeviceClient deviceClient;
        readonly string deviceId;
        readonly IotHubClientSettings settings;
        readonly IByteBufferAllocator allocator;
        readonly IMessageAddressConverter messageAddressConverter;
        IMessagingChannel<IMessage> messagingChannel;

        IotHubClient(DeviceClient deviceClient, string deviceId, IotHubClientSettings settings, IByteBufferAllocator allocator, IMessageAddressConverter messageAddressConverter)
        {
            this.deviceClient = deviceClient;
            this.deviceId = deviceId;
            this.settings = settings;
            this.allocator = allocator;
            this.messageAddressConverter = messageAddressConverter;
        }

        public static async Task<IMessagingServiceClient> CreateFromConnectionStringAsync(string deviceId, string connectionString,
            int connectionPoolSize, TimeSpan? connectionIdleTimeout, IotHubClientSettings settings, IByteBufferAllocator allocator, IMessageAddressConverter messageAddressConverter)
        {
            int maxPendingOutboundMessages = settings.MaxPendingOutboundMessages;
            var tcpSettings = new AmqpTransportSettings(TransportType.Amqp_Tcp_Only);
            var webSocketSettings = new AmqpTransportSettings(TransportType.Amqp_WebSocket_Only);
            webSocketSettings.PrefetchCount = tcpSettings.PrefetchCount = (uint)maxPendingOutboundMessages;
            if (connectionPoolSize > 0)
            {
                var amqpConnectionPoolSettings = new AmqpConnectionPoolSettings
                {
                    MaxPoolSize = unchecked ((uint)connectionPoolSize),
                    Pooling = connectionPoolSize > 0
                };
                if (connectionIdleTimeout.HasValue)
                {
                    amqpConnectionPoolSettings.ConnectionIdleTimeout = connectionIdleTimeout.Value;
                }
                tcpSettings.AmqpConnectionPoolSettings = amqpConnectionPoolSettings;
                webSocketSettings.AmqpConnectionPoolSettings = amqpConnectionPoolSettings;
            }
            DeviceClient client = DeviceClient.CreateFromConnectionString(connectionString, new ITransportSettings[]
            {
                tcpSettings,
                webSocketSettings
            });
            try
            {
                await client.OpenAsync();
            }
            catch (IotHubException ex)
            {
                throw ComposeIotHubCommunicationException(ex);
            }
            return new IotHubClient(client, deviceId, settings, allocator, messageAddressConverter);
        }

        public static Func<IDeviceIdentity, Task<IMessagingServiceClient>> PreparePoolFactory(string baseConnectionString, int connectionPoolSize,
            TimeSpan? connectionIdleTimeout, IotHubClientSettings settings, IByteBufferAllocator allocator, IMessageAddressConverter messageAddressConverter)
        {
            IotHubConnectionStringBuilder csb = IotHubConnectionStringBuilder.Create(baseConnectionString);
            Func<IDeviceIdentity, Task<IMessagingServiceClient>> mqttCommunicatorFactory = deviceIdentity =>
            {
                var identity = (IotHubDeviceIdentity)deviceIdentity;
                csb.AuthenticationMethod = DeriveAuthenticationMethod(csb.AuthenticationMethod, identity);
                csb.HostName = identity.IotHubHostName;
                string connectionString = csb.ToString();
                return CreateFromConnectionStringAsync(identity.Id, connectionString, connectionPoolSize, connectionIdleTimeout, settings, allocator, messageAddressConverter);
            };
            return mqttCommunicatorFactory;
        }

        public int MaxPendingMessages => this.settings.MaxPendingInboundMessages;

        public IMessage CreateMessage(string address, IByteBuffer payload)
        {
            var message = new IotHubClientMessage(new Message(payload.IsReadable() ? new ReadOnlyByteBufferStream(payload, false) : null), payload);
            message.Address = address;
            return message;
        }

        public void BindMessagingChannel(IMessagingChannel<IMessage> channel)
        {
            Contract.Requires(channel != null);

            Contract.Assert(this.messagingChannel == null);

            this.messagingChannel = channel;
            this.Receive();
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
                        if (!this.deviceId.Equals(messageDeviceId, StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException(
                                $"Device ID provided in topic name ({messageDeviceId}) does not match ID of the device publishing message ({this.deviceId})");
                        }
                        message.Properties.Remove(TemplateParameters.DeviceIdTemplateParam);
                    }
                }
                else
                {
                    if (!this.settings.PassThroughUnmatchedMessages)
                    {
                        throw new InvalidOperationException($"Topic name `{address}` could not be matched against any of the configured routes.");
                    }

                    CommonEventSource.Log.Warning("Topic name could not be matched against any of the configured routes. Falling back to default telemetry settings.", address);
                    message.Properties[this.settings.ServicePropertyPrefix + MessagePropertyNames.Unmatched] = bool.TrueString;
                    message.Properties[this.settings.ServicePropertyPrefix + MessagePropertyNames.Subject] = address;
                }
                Message iotHubMessage = clientMessage.ToMessage();
                
                await this.deviceClient.SendEventAsync(iotHubMessage);
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

                    this.messagingChannel.Handle(msg);

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

        public async Task DisposeAsync(Exception cause)
        {
            try
            {
                await this.deviceClient.CloseAsync();
            }
            catch (IotHubException ex)
            {
                throw ComposeIotHubCommunicationException(ex);
            }
        }

        internal static IAuthenticationMethod DeriveAuthenticationMethod(IAuthenticationMethod currentAuthenticationMethod, IotHubDeviceIdentity deviceIdentity)
        {
            switch (deviceIdentity.Scope)
            {
                case AuthenticationScope.None:
                    var policyKeyAuth = currentAuthenticationMethod as DeviceAuthenticationWithSharedAccessPolicyKey;
                    if (policyKeyAuth != null)
                    {
                        return new DeviceAuthenticationWithSharedAccessPolicyKey(deviceIdentity.Id, policyKeyAuth.PolicyName, policyKeyAuth.Key);
                    }
                    var deviceKeyAuth = currentAuthenticationMethod as DeviceAuthenticationWithRegistrySymmetricKey;
                    if (deviceKeyAuth != null)
                    {
                        return new DeviceAuthenticationWithRegistrySymmetricKey(deviceIdentity.Id, deviceKeyAuth.DeviceId);
                    }
                    var deviceTokenAuth = currentAuthenticationMethod as DeviceAuthenticationWithToken;
                    if (deviceTokenAuth != null)
                    {
                        return new DeviceAuthenticationWithToken(deviceIdentity.Id, deviceTokenAuth.Token);
                    }
                    throw new InvalidOperationException("");
                case AuthenticationScope.SasToken:
                    return new DeviceAuthenticationWithToken(deviceIdentity.Id, deviceIdentity.Secret);
                case AuthenticationScope.DeviceKey:
                    return new DeviceAuthenticationWithRegistrySymmetricKey(deviceIdentity.Id, deviceIdentity.Secret);
                case AuthenticationScope.HubKey:
                    return new DeviceAuthenticationWithSharedAccessPolicyKey(deviceIdentity.Id, deviceIdentity.PolicyName, deviceIdentity.Secret);
                default:
                    throw new InvalidOperationException("Unexpected AuthenticationScope value: " + deviceIdentity.Scope);
            }
        }

        static MessagingException ComposeIotHubCommunicationException(IotHubException ex)
        {
            return new MessagingException(ex.Message, ex.InnerException, ex.IsTransient, ex.TrackingId);
        }
    }
}