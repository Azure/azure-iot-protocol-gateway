// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Tests.Load
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Linq;
    using System.Net;
    using System.Net.Security;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Mqtt;
    using DotNetty.Codecs.Mqtt.Packets;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Handlers.Tls;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using Microsoft.Azure.Devices.Common.Security;

    abstract class DeviceRunner
    {
        const int MaxPayloadSize = 16 * 1024;
        static readonly ThreadLocal<Random> ThreadLocalRandom = new ThreadLocal<Random>(() => new Random((int)DateTime.UtcNow.ToFileTimeUtc()));
        static readonly byte[][] Payloads = GeneratePayloads(MaxPayloadSize);
        static readonly TimeSpan CommunicationTimeout = TimeSpan.FromSeconds(60);

        readonly IotHubConnectionStringBuilder connectionStringBuilder;
        readonly string deviceKey;
        readonly IPEndPoint endpoint;
        readonly string tlsHostName;
        readonly Bootstrap bootstrap;

        protected DeviceRunner(IEventLoopGroup eventLoopGroup, string deviceKey, string iotHubConnectionString, IPEndPoint endpoint, string tlsHostName)
        {
            this.deviceKey = deviceKey;
            this.endpoint = endpoint;
            this.tlsHostName = tlsHostName;
            this.connectionStringBuilder = IotHubConnectionStringBuilder.Create(iotHubConnectionString.Contains("DeviceId=") ? iotHubConnectionString : iotHubConnectionString + ";DeviceId=abc");
            this.bootstrap = new Bootstrap()
                .Group(eventLoopGroup)
                .Channel<TcpSocketChannel>()
                .Option(ChannelOption.TcpNodelay, false);
        }

        public async Task<Task> StartAsync(string deviceId, CancellationToken cancellationToken)
        {
            string deviceSas =
                new SharedAccessSignatureBuilder
                {
                    Key = this.deviceKey,
                    Target = $"{this.connectionStringBuilder.HostName}/devices/{deviceId}",
                    TimeToLive = TimeSpan.FromDays(3)
                }
                    .ToSignature();

            var taskCompletionSource = new TaskCompletionSource();
            Bootstrap b = this.bootstrap.Clone();
            var readHandler = new ReadListeningHandler(CommunicationTimeout);
            b.Handler(new ActionChannelInitializer<ISocketChannel>(
                ch =>
                {
                    ch.Pipeline.AddLast(
                        new TlsHandler(stream => new SslStream(stream, true, (sender, certificate, chain, errors) => true), new ClientTlsSettings(this.tlsHostName)),
                        MqttEncoder.Instance,
                        new MqttDecoder(false, 256 * 1024),
                        readHandler,
                        ConsoleLoggingHandler.Instance);
                }));

            var channel = await b.ConnectAsync(this.endpoint);

            await this.GetCompleteScenarioAsync(channel, readHandler, deviceId, deviceSas, cancellationToken);
            
            return taskCompletionSource.Task;
        }

        public virtual async Task<bool> OnClosedAsync(string deviceId, Exception exception, bool onStart)
        {
            Console.WriteLine("Error running client: id: {0}, onStart: {1}, exception({2}): {3}", deviceId, onStart, exception.GetType().Name, exception.Message);
            await Task.Delay(TimeSpan.FromSeconds(60));
            return true;
        }

        async Task GetCompleteScenarioAsync(IChannel channel, ReadListeningHandler readHandler, string clientId, string password, CancellationToken cancellationToken)
        {
            await channel.WriteAndFlushAsync(new ConnectPacket
            {
                ClientId = clientId,
                CleanSession = false,
                HasUsername = true,
                Username = clientId,
                HasPassword = true,
                Password = password,
                KeepAliveInSeconds = 120
            });

            object message = await readHandler.ReceiveAsync();
            var connackPacket = message as ConnAckPacket;
            if (connackPacket == null)
            {
                throw new InvalidOperationException(string.Format("{1}: Expected CONNACK, received {0}", message, clientId));
            }
            else if (connackPacket.ReturnCode != ConnectReturnCode.Accepted)
            {
                throw new InvalidOperationException(string.Format("{1}: Expected successful CONNACK, received CONNACK with return code of {0}", connackPacket.ReturnCode, clientId));
            }

            await this.GetScenario(channel, readHandler, clientId, cancellationToken);
        }

        protected virtual Task GetScenario(IChannel channel, ReadListeningHandler readHandler, string clientId,
            CancellationToken cancellationToken)
        {
            return TaskEx.Completed;
        }

        protected abstract string Name { get; }

        public RunnerConfiguration CreateConfiguration(int count, TimeSpan rampUpPeriod)
        {
            return new RunnerConfiguration(this.Name, this.StartAsync, this.OnClosedAsync, count, rampUpPeriod);
        }

        #region scenario composition utils

        /// <summary>
        ///     Thread local Random object.
        /// </summary>
        protected static Random Random => ThreadLocalRandom.Value;

        static byte[][] GeneratePayloads(int maxSize)
        {
            var result = new byte[maxSize + 1][];
            for (int i = 0; i <= maxSize; i++)
            {
                var item = new byte[i];
                Random.NextBytes(item);
                result[i] = item;
            }
            return result;
        }

        protected static IByteBuffer GetPayloadBuffer(int size)
        {
            Contract.Requires(size < MaxPayloadSize);

            return Unpooled.WrappedBuffer(Payloads[size]);
        }

        protected static Task GetSubscribeSteps(IChannel channel, ReadListeningHandler readHandler, string clientId)
        {
            return GetSubscribeSteps(channel, readHandler, clientId, "devices/{0}/messages/devicebound");
        }

        protected static async Task GetSubscribeSteps(IChannel channel, ReadListeningHandler readHandler, string clientId, string topicNamePattern)
        {
            int subscribePacketId = Random.Next(1, ushort.MaxValue);
            await channel.WriteAndFlushAsync(
                new SubscribePacket(
                    subscribePacketId,
                    new SubscriptionRequest(string.Format(topicNamePattern, clientId), QualityOfService.ExactlyOnce)));
            object message = await readHandler.ReceiveAsync();
            var subackPacket = message as SubAckPacket;
            if (subackPacket == null)
            {
                throw new InvalidOperationException(string.Format("{1}: Expected SUBACK, received {0}", message, clientId));
            }
            else if (subackPacket.PacketId == subscribePacketId && subackPacket.ReturnCodes[0] > QualityOfService.ExactlyOnce)
            {
                throw new InvalidOperationException($"{clientId}: Expected successful SUBACK({subscribePacketId.ToString()}), received SUBACK({subackPacket.PacketId.ToString()}) with QoS={subackPacket.ReturnCodes[0].ToString()}");
            }
        }

        protected static async Task GetPublishSteps(IChannel channel, ReadListeningHandler readHandler, string clientId, QualityOfService qos,
            string topicNamePattern, int count, int minPayloadSize, int maxPayloadSize)
        {
            Contract.Requires(count > 0);
            Contract.Requires(qos < QualityOfService.ExactlyOnce);

            PublishPacket[] publishPackets = Enumerable.Repeat(0, count)
                .Select(_ => new PublishPacket(qos, false, false)
                {
                    TopicName = string.Format(topicNamePattern, clientId),
                    PacketId = Random.Next(1, ushort.MaxValue),
                    Payload = GetPayloadBuffer(Random.Next(minPayloadSize, maxPayloadSize))
                })
                .ToArray();
            await channel.WriteAndFlushManyAsync(publishPackets);

            if (qos == QualityOfService.AtMostOnce)
            {
                return;
            }

            int acked = 0;
            do
            {
                object receivedMessage = await readHandler.ReceiveAsync();
                var pubackPacket = receivedMessage as PubAckPacket;
                if (pubackPacket == null || pubackPacket.PacketId != publishPackets[acked].PacketId)
                {
                    throw new InvalidOperationException($"{clientId}: Expected PUBACK({publishPackets[acked].PacketId.ToString()}), received {receivedMessage}");
                }

                acked++;
            }
            while (acked < count);
        }

        #endregion
    }
}