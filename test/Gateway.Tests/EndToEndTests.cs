// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.Gateway.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Diagnostics;
    using System.Diagnostics.Tracing;
    using System.Linq;
    using System.Net;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Mqtt;
    using DotNetty.Codecs.Mqtt.Packets;
    using DotNetty.Common.Concurrency;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using Microsoft.Azure.Devices.Client;
    using DotNetty.Transport.Channels.Sockets;
    using Microsoft.Azure.Devices.Gateway.Cloud;
    using Microsoft.Azure.Devices.Gateway.Core;
    using Microsoft.Azure.Devices.Gateway.Core.Mqtt;
    using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;
    using Microsoft.ServiceBus.Messaging;
    using Moq;
    using Xunit;
    using Xunit.Abstractions;

    public class EndToEndTests : IDisposable
    {
        readonly ITestOutputHelper output;
        readonly ISettingsProvider settingsProvider;
        Func<Task> cleanupFunc;
        readonly ObservableEventListener eventListener;
        readonly string deviceId;
        readonly string deviceKey;

        public EndToEndTests(ITestOutputHelper output)
        {
            this.output = output;
            this.eventListener = new ObservableEventListener();
            this.eventListener.LogToTestOutput(output);
            this.eventListener.EnableEvents(BridgeEventSource.Log, EventLevel.Verbose);
            this.eventListener.EnableEvents(ChannelEventSource.Log, EventLevel.Verbose);
            this.eventListener.EnableEvents(BootstrapEventSource.Log, EventLevel.Verbose);
            this.eventListener.EnableEvents(ExecutorEventSource.Log, EventLevel.Verbose);
            this.eventListener.EnableEvents(MqttEventSource.Log, EventLevel.Verbose);

            this.settingsProvider = new DefaultSettingsProvider();
            this.deviceId = ConfigurationManager.AppSettings["End2End.DeviceName"];
            this.deviceKey = ConfigurationManager.AppSettings["End2End.DeviceKey"];

            this.ProtocolGatewayPort = 8883; // todo: dynamic port choice to parallelize test runs (first free port)

            string serverAddress;
            IPAddress serverIp;
            if (this.settingsProvider.TryGetSetting("End2End.ServerAddress", out serverAddress) && IPAddress.TryParse(serverAddress, out serverIp))
            {
                this.ServerAddress = serverIp;
            }
        }

        async Task EnsureServerInitializedAsync()
        {
            if (this.ServerAddress != null)
            {
                return;
            }

            int threadCount = Environment.ProcessorCount;
            var executorGroup = new MultithreadEventLoopGroup(threadCount);
            var bufAllocator = new PooledByteBufferAllocator(16 * 1024, 10 * 1024 * 1024 / threadCount); // reserve 10 MB for 64 KB buffers
            BlobSessionStateManager sessionStateManager = await BlobSessionStateManager.CreateAsync(this.settingsProvider.GetSetting("SessionStateManager.StorageConnectionString"), this.settingsProvider.GetSetting("SessionStateManager.StorageContainerName"));
            var certificate = new X509Certificate2("tlscert.pfx", "password");
            var settings = new Settings(this.settingsProvider);
            //var authProvider = new PassThroughAuthenticationProvider();
            var authProviderMock = new Mock<IAuthenticationProvider>();
            authProviderMock.Setup(x => x.AuthenticateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EndPoint>()))
                .Returns((string clientId, string userName, string password, EndPoint remoteAddress) => Task.FromResult(AuthenticationResult.SuccessWithDefaultCredentials(clientId)));
            IAuthenticationProvider authProvider = authProviderMock.Object;
            var topicNameRouter = new TopicNameRouter();

            ServerBootstrap server = new ServerBootstrap()
                .Group(executorGroup)
                .Channel<TcpServerSocketChannel>()
                .ChildOption(ChannelOption.Allocator, bufAllocator)
                .ChildOption(ChannelOption.AutoRead, false)
                .ChildHandler(new ActionChannelInitializer<IChannel>(ch =>
                {
                    //ch.Pipeline.AddLast(new TlsHandler(certificate));
                    ch.Pipeline.AddLast(
                        MqttEncoder.Instance,
                        new MqttDecoder(true, 256 * 1024),
                        new BridgeDriver(settings, sessionStateManager, authProvider, topicNameRouter));
                }));

            IChannel serverChannel = await server.BindAsync(IPAddress.Any, this.ProtocolGatewayPort);

            this.ScheduleCleanup(async () =>
            {
                await serverChannel.CloseAsync();
                await executorGroup.ShutdownGracefullyAsync();
            });
            this.ServerAddress = IPAddress.Loopback;
        }

        void ScheduleCleanup(Func<Task> cleanupFunc)
        {
            Func<Task> currentCleanupFunc = this.cleanupFunc;
            this.cleanupFunc = async () =>
            {
                if (currentCleanupFunc != null)
                {
                    await currentCleanupFunc();
                }

                await cleanupFunc();
            };
        }

        public IPAddress ServerAddress { get; set; }

        public int ProtocolGatewayPort { get; set; }

        public ServerBootstrap Server { get; set; }

        public void Dispose()
        {
            if (this.cleanupFunc != null)
            {
                if (!this.cleanupFunc().Wait(TimeSpan.FromSeconds(10)))
                {
                    this.output.WriteLine("Server cleanup did not complete in time.");
                }
            }
            this.eventListener.Dispose();
        }

        [Fact(Skip = "just a handy method to calc remaining length for MQTT packet")]
        public void CalcMqttRemainingLength()
        {
            int value = 10;
            do
            {
                int encodedByte = value % 128;
                value = value / 128;
                // if there are more data to encode, set the top bit of this byte 286
                if (value > 0)
                {
                    encodedByte = encodedByte | 128;
                }
                Console.WriteLine(encodedByte.ToString("X"));
            }
            while (value > 0);
        }

        /// <summary>
        /// Tests sequence of actions:
        /// - CONNECT (incl. loading of session state, connection to IoT Hub) " +
        /// - SUBSCRIBE (incl. saving changes to session state)" +
        /// - PUBLISH QoS 0, QoS 1 from device (incl. verification through Event Hub on receiving end)" +
        /// - PUBLISH QoS 0, QoS 1 to device" +
        /// - DISCONNECT
        /// </summary>
        [Fact]
        public async Task BasicFunctionalityTest()
        {
            await this.EnsureServerInitializedAsync();

            const string TelemetryQoS0Content = "{\"test\": \"telemetry-QoS0\"}";
            const string TelemetryQoS1Content = "{\"test\": \"telemetry\"}";
            const string NotificationQoS0Content = "{\"test\": \"fire and forget\"}";
            const string NotificationQoS1Content = "{\"test\": \"notify (at least once)\"}";

            int protocolGatewayPort = this.ProtocolGatewayPort;

            DeviceClient deviceClient = DeviceClient.CreateFromConnectionString(ConfigurationManager.AppSettings["IoTHub.ConnectionString"], this.deviceId);

            Message staleMessage;
            while ((staleMessage = await deviceClient.ReceiveAsync(TimeSpan.Zero)) != null)
            {
                await deviceClient.CompleteAsync(staleMessage);
            }

            EventHubClient eventHubClient = EventHubClient.CreateFromConnectionString(ConfigurationManager.AppSettings["EventHub.ConnectionString"]);
            EventHubConsumerGroup ehGroup = eventHubClient.GetDefaultConsumerGroup();
            string[] partitionIds = ConfigurationManager.AppSettings["EventHub.Partitions"].Split(',');
            EventHubReceiver[] receivers = await Task.WhenAll(partitionIds.Select(pid => ehGroup.CreateReceiverAsync(pid.Trim(), DateTime.UtcNow)));

            ServiceClient serviceClient = ServiceClient.CreateFromConnectionString(ConfigurationManager.AppSettings["IoTHub.ConnectionString"]);

            Stopwatch sw = Stopwatch.StartNew();

            var testPromise = new TaskCompletionSource();

            var group = new MultithreadEventLoopGroup();
            Bootstrap bootstrap = new Bootstrap()
                .Group(group)
                .Channel<TcpSocketChannel>()
                .Option(ChannelOption.TcpNodelay, true)
                .Handler(new ActionChannelInitializer<ISocketChannel>(ch => ch.Pipeline.AddLast(
                    MqttEncoder.Instance,
                    new MqttDecoder(false, 256 * 1024),
                    new TestScenarioRunner(cmf => GetClientScenario(cmf, this.deviceId, this.deviceKey), testPromise))));
            IChannel clientChannel = await bootstrap.ConnectAsync(this.ServerAddress, protocolGatewayPort);
            this.ScheduleCleanup(async () =>
            {
                await clientChannel.CloseAsync();
                await group.ShutdownGracefullyAsync();
            });

            var ehMessages = new Tuple<EventData, string>[2];
            List<Task<EventData>> receiveTasks = receivers.Select(r => r.ReceiveAsync(TimeSpan.FromMinutes(20))).ToList();
            int messagesPending = 2;
            while (true)
            {
                Task<EventData> receivedTask = await Task.WhenAny(receiveTasks);
                EventData eventData = receivedTask.Result;
                if (eventData != null)
                {
                    ehMessages[messagesPending - 1] = Tuple.Create(eventData, Encoding.UTF8.GetString(eventData.GetBytes()));

                    if (--messagesPending == 0)
                    {
                        break;
                    }
                }

                int receivedIndex = receiveTasks.IndexOf(receivedTask);
                receiveTasks[receivedIndex] = receivers[receivedIndex].ReceiveAsync(TimeSpan.FromMinutes(20));
            }

            Tuple<EventData, string> qos0Event = Assert.Single(ehMessages.Where(x => TelemetryQoS0Content.Equals(x.Item2, StringComparison.Ordinal)));
            Assert.Equal((string)qos0Event.Item1.Properties["level"], "verbose");
            Assert.Equal((string)qos0Event.Item1.Properties["subject"], "n/a");

            Tuple<EventData, string> qos1Event = Assert.Single(ehMessages.Where(x => TelemetryQoS1Content.Equals(x.Item2, StringComparison.Ordinal)));

            var qos0Notification = new Microsoft.Azure.Devices.Message(Encoding.UTF8.GetBytes(NotificationQoS0Content));
            qos0Notification.Properties[ConfigurationManager.AppSettings["IoTHub.QoSProperty"]] = "0";
            qos0Notification.Properties["subTopic"] = "tips";
            await serviceClient.SendAsync(this.deviceId, qos0Notification);
            var qos1Notification = new Microsoft.Azure.Devices.Message(Encoding.UTF8.GetBytes(NotificationQoS1Content));
            qos1Notification.Properties["subTopic"] = "firmware-update";
            await serviceClient.SendAsync(this.deviceId, qos1Notification);

            await Task.WhenAny(testPromise.Task, Task.Delay(TimeSpan.FromMinutes(200)));

            Assert.True(testPromise.Task.IsCompleted, "Test has timed out.");

            this.output.WriteLine(string.Format("Core test completed in {0}", sw.Elapsed));

            // making sure that device queue is empty.
            await Task.Delay(TimeSpan.FromSeconds(3));
            Message message = await deviceClient.ReceiveAsync(TimeSpan.FromSeconds(1));
            Assert.True(message == null, string.Format("Message is not null: {0}", message));
        }

        static IEnumerable<TestScenarioStep> GetClientScenario(Func<object> currentMessageFunc, string clientId, string password)
        {
            yield return TestScenarioStep.Message(new ConnectPacket
            {
                ClientId = clientId,
                Username = clientId,
                Password = password,
                KeepAliveInSeconds = 120,
                WillTopicName = "last/word",
                WillMessage = Unpooled.WrappedBuffer(Encoding.UTF8.GetBytes("oops"))
            });

            var connAckPacket = Assert.IsType<ConnAckPacket>(currentMessageFunc());
            Assert.Equal(ConnectReturnCode.Accepted, connAckPacket.ReturnCode);

            int subscribePacketId = GetRandomPacketId();
            yield return TestScenarioStep.Message(new SubscribePacket
            {
                PacketId = subscribePacketId,
                Requests = new[]
                {
                    new SubscriptionRequest(string.Format("devices/{0}/messages/devicebound/#", clientId), QualityOfService.ExactlyOnce)
                }
            });

            var subAckPacket = Assert.IsType<SubAckPacket>(currentMessageFunc());
            Assert.Equal(subscribePacketId, subAckPacket.PacketId);
            Assert.Equal(1, subAckPacket.ReturnCodes.Count);
            Assert.Equal(QualityOfService.AtLeastOnce, subAckPacket.ReturnCodes[0]);

            int publishQoS1PacketId = GetRandomPacketId();
            yield return TestScenarioStep.Messages(
                new PublishPacket(QualityOfService.AtMostOnce, false, false)
                {
                    TopicName = string.Format("devices/{0}/messages/log/verbose/", clientId),
                    Payload = Unpooled.WrappedBuffer(Encoding.UTF8.GetBytes("{\"test\": \"telemetry-QoS0\"}"))
                },
                new PublishPacket(QualityOfService.AtLeastOnce, false, false)
                {
                    PacketId = publishQoS1PacketId,
                    TopicName = string.Format("devices/{0}/messages/events", clientId),
                    Payload = Unpooled.WrappedBuffer(Encoding.UTF8.GetBytes("{\"test\": \"telemetry\"}"))
                });
            //new PublishPacket(QualityOfService.AtLeastOnce, false, false) { TopicName = "feedback/qos/One", Payload = Unpooled.WrappedBuffer(Encoding.UTF8.GetBytes("QoS 1 test. Different data length.")) });

            var packets = new Packet[3];
            for (int i = packets.Length - 1; i >= 0; i--)
            {
                packets[i] = Assert.IsAssignableFrom<Packet>(currentMessageFunc());
                if (i > 0)
                {
                    yield return TestScenarioStep.MoreFeedbackExpected();
                }
            }

            PubAckPacket pubAckPacket = Assert.Single(packets.OfType<PubAckPacket>());
            Assert.Equal(publishQoS1PacketId, pubAckPacket.PacketId);

            PublishPacket publishQoS0Packet = Assert.Single(packets.OfType<PublishPacket>().Where(x => x.QualityOfService == QualityOfService.AtMostOnce));
            Assert.Equal(string.Format("devices/{0}/messages/devicebound/tips", clientId), publishQoS0Packet.TopicName);
            Assert.Equal("{\"test\": \"fire and forget\"}", Encoding.UTF8.GetString(publishQoS0Packet.Payload.ToArray()));

            PublishPacket publishQoS1Packet = Assert.Single(packets.OfType<PublishPacket>().Where(x => x.QualityOfService == QualityOfService.AtLeastOnce));
            Assert.Equal(string.Format("devices/{0}/messages/devicebound/firmware-update", clientId), publishQoS1Packet.TopicName);
            Assert.Equal("{\"test\": \"notify (at least once)\"}", Encoding.UTF8.GetString(publishQoS1Packet.Payload.ToArray()));

            yield return TestScenarioStep.Messages(
                PubAckPacket.InResponseTo(publishQoS1Packet),
                DisconnectPacket.Instance);
        }

        static int GetRandomPacketId()
        {
            return Guid.NewGuid().GetHashCode() & ushort.MaxValue;
        }
    }
}