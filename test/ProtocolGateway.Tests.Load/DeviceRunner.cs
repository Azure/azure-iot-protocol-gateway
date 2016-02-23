// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Gateway.Tests.Load
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Mqtt;
    using DotNetty.Codecs.Mqtt.Packets;
    using DotNetty.Common.Concurrency;
    using DotNetty.Handlers.Tls;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Common.Security;
    using Microsoft.Azure.Devices.Gateway.Tests;

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
                    Target = string.Format("{0}/devices/{1}", this.connectionStringBuilder.HostName, deviceId),
                    TimeToLive = TimeSpan.FromDays(3)
                }
                    .ToSignature();

            var taskCompletionSource = new TaskCompletionSource();
            var b = (Bootstrap)this.bootstrap.Clone();
            b.Handler(new ActionChannelInitializer<ISocketChannel>(
                ch =>
                {
                    ch.Pipeline.AddLast(
                        TlsHandler.Client(this.tlsHostName, null, (sender, certificate, chain, errors) => true),
                        MqttEncoder.Instance,
                        new MqttDecoder(false, 256 * 1024),
                        new TestScenarioRunner(
                            cmf => this.GetCompleteScenario(cmf, deviceId, deviceSas, cancellationToken),
                            taskCompletionSource,
                            CommunicationTimeout,
                            CommunicationTimeout),
                        ConsoleLoggingHandler.Instance);
                }));

            await b.ConnectAsync(this.endpoint);

            return taskCompletionSource.Task;
        }

        public virtual async Task<bool> OnClosedAsync(string deviceId, Exception exception, bool onStart)
        {
            Console.WriteLine("Error running client: id: {0}, onStart: {1}, exception({2}): {3}", deviceId, onStart, exception.GetType().Name, exception.Message);
            await Task.Delay(TimeSpan.FromSeconds(60));
            return true;
        }

        IEnumerable<TestScenarioStep> GetCompleteScenario(Func<object> currentMessageFunc, string clientId, string password, CancellationToken cancellationToken)
        {
            yield return TestScenarioStep.Write(new ConnectPacket
            {
                ClientId = clientId,
                CleanSession = false,
                HasUsername = true,
                Username = clientId,
                HasPassword = true,
                Password = password,
                KeepAliveInSeconds = 120
            });

            object message = currentMessageFunc();
            var connackPacket = message as ConnAckPacket;
            if (connackPacket == null)
            {
                throw new InvalidOperationException(string.Format("{1}: Expected CONNACK, received {0}", message, clientId));
            }
            else if (connackPacket.ReturnCode != ConnectReturnCode.Accepted)
            {
                throw new InvalidOperationException(string.Format("{1}: Expected successful CONNACK, received CONNACK with return code of {0}", connackPacket.ReturnCode, clientId));
            }

            foreach (TestScenarioStep step in this.GetScenario(currentMessageFunc, clientId, cancellationToken))
            {
                yield return step;
            }
        }

        protected virtual IEnumerable<TestScenarioStep> GetScenario(Func<object> currentMessageFunc, string clientId,
            CancellationToken cancellationToken)
        {
            return this.GetScenarioNested(currentMessageFunc, clientId, cancellationToken).SelectMany(_ => _);
        }

        protected virtual IEnumerable<IEnumerable<TestScenarioStep>> GetScenarioNested(Func<object> currentMessageFunc, string clientId, CancellationToken cancellationToken)
        {
            yield break;
        }

        protected abstract string Name { get; }

        public RunnerConfiguration CreateConfiguration(int count, TimeSpan rampUpPeriod)
        {
            return new RunnerConfiguration(this.Name, this.StartAsync, this.OnClosedAsync, count, rampUpPeriod);
        }

        #region scenario composition utils

        /// <summary>
        /// Thread local Random object.
        /// </summary>
        protected static Random Random
        {
            get { return ThreadLocalRandom.Value; }
        }

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

        protected static IEnumerable<TestScenarioStep> GetSubscribeSteps(Func<object> currentMessageFunc, string clientId)
        {
            return GetSubscribeSteps(currentMessageFunc, clientId, "devices/{0}/messages/devicebound");
        }

        protected static IEnumerable<TestScenarioStep> GetSubscribeSteps(Func<object> currentMessageFunc, string clientId, string topicNamePattern)
        {
            int subscribePacketId = Random.Next(1, ushort.MaxValue);
            yield return TestScenarioStep.Write(
                new SubscribePacket(
                    subscribePacketId,
                    new SubscriptionRequest(string.Format(topicNamePattern, clientId), QualityOfService.ExactlyOnce)));
            object message = currentMessageFunc();
            var subackPacket = message as SubAckPacket;
            if (subackPacket == null)
            {
                throw new InvalidOperationException(string.Format("{1}: Expected SUBACK, received {0}", message, clientId));
            }
            else if (subackPacket.PacketId == subscribePacketId && subackPacket.ReturnCodes[0] > QualityOfService.ExactlyOnce)
            {
                throw new InvalidOperationException(string.Format("{3}: Expected successful SUBACK({0}), received SUBACK({1}) with QoS={2}",
                    subscribePacketId, subackPacket.PacketId, subackPacket.ReturnCodes[0], clientId));
            }
        }

        protected static IEnumerable<TestScenarioStep> GetPublishSteps(Func<object> currentMessageFunc, string clientId, QualityOfService qos,
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
            yield return TestScenarioStep.Write(qos > QualityOfService.AtMostOnce, publishPackets);

            if (qos == QualityOfService.AtMostOnce)
            {
                yield break;
            }

            int acked = 0;
            do
            {
                object receivedMessage = currentMessageFunc();
                var pubackPacket = receivedMessage as PubAckPacket;
                if (pubackPacket == null || pubackPacket.PacketId != publishPackets[acked].PacketId)
                {
                    throw new InvalidOperationException(string.Format("{0}: Expected PUBACK({1}), received {2}",
                        clientId, publishPackets[acked].PacketId, receivedMessage));
                }

                if (acked < count - 1)
                {
                    yield return TestScenarioStep.ReadMore();
                }

                acked++;
            }
            while (acked < count);
        }

        #endregion
    }
}