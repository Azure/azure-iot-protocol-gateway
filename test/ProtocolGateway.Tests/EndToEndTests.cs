// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Tests
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
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Handlers.Logging;
    using DotNetty.Handlers.Tls;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using global::ProtocolGateway.Host.Common;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Common.Security;
    using Microsoft.Azure.Devices.Gateway.Tests;
    using Microsoft.Azure.Devices.ProtocolGateway.Instrumentation;
    using Microsoft.Azure.Devices.ProtocolGateway.IotHubClient;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Routing;
    using Microsoft.Azure.Devices.ProtocolGateway.Providers.CloudStorage;
    using Microsoft.Azure.Devices.ProtocolGateway.Tests.Extensions;
    using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;
    using Microsoft.ServiceBus.Messaging;
    using Xunit;
    using Xunit.Abstractions;
    using IotHubConnectionStringBuilder = Microsoft.Azure.Devices.IotHubConnectionStringBuilder;
    using Message = Microsoft.Azure.Devices.Message;

    public class EndToEndTests : IDisposable
    {
        const string TelemetryQoS0Content = "{\"test\": \"telemetry-QoS0\"}";
        const string TelemetryQoS1Content = "{\"test\": \"telemetry\"}";
        const string NotificationQoS0Content = "{\"test\": \"fire and forget\"}";
        const string NotificationQoS1Content = "{\"test\": \"notify (at least once)\"}";
        const string NotificationQoS2Content = "{\"test\": \"exactly once\"}";
        const string NotificationQoS2Content2 = "{\"test\": \"exactly once (2)\"}";

        readonly ITestOutputHelper output;
        readonly ISettingsProvider settingsProvider;
        Func<Task> cleanupFunc;
        readonly ObservableEventListener eventListener;
        string deviceId;
        string deviceSas;
        readonly X509Certificate2 tlsCertificate;
        static readonly TimeSpan CommunicationTimeout = TimeSpan.FromSeconds(15);
        static readonly TimeSpan TestTimeout = TimeSpan.FromMinutes(5); //TimeSpan.FromMinutes(1);

        public EndToEndTests(ITestOutputHelper output)
        {
            this.output = output;
            this.eventListener = new ObservableEventListener();
            this.eventListener.LogToTestOutput(output);
            this.eventListener.EnableEvents(MqttIotHubAdapterEventSource.Log, EventLevel.Verbose);
            this.eventListener.EnableEvents(DefaultEventSource.Log, EventLevel.Verbose);
            this.eventListener.EnableEvents(BootstrapperEventSource.Log, EventLevel.Verbose);

            this.settingsProvider = new AppConfigSettingsProvider();

            this.ProtocolGatewayPort = 8883; // todo: dynamic port choice to parallelize test runs (first free port)

            this.tlsCertificate = new X509Certificate2("tlscert.pfx", "password");

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
            BlobSessionStatePersistenceProvider sessionStateProvider = await BlobSessionStatePersistenceProvider.CreateAsync(
                this.settingsProvider.GetSetting("BlobSessionStatePersistenceProvider.StorageConnectionString"),
                this.settingsProvider.GetSetting("BlobSessionStatePersistenceProvider.StorageContainerName"));
            TableQos2StatePersistenceProvider qos2StateProvider = await TableQos2StatePersistenceProvider.CreateAsync(
                this.settingsProvider.GetSetting("TableQos2StatePersistenceProvider.StorageConnectionString"),
                this.settingsProvider.GetSetting("TableQos2StatePersistenceProvider.StorageTableName"));
            var settings = new Settings(this.settingsProvider);
            var authProvider = new SasTokenDeviceIdentityProvider();
            var topicNameRouter = new ConfigurableMessageRouter();

            IotHubClientFactoryFunc iotHubClientFactoryMethod = IotHubClient.PreparePoolFactory(settings.IotHubConnectionString + ";DeviceId=stub", "a", 1);
            var iotHubFactory = new IotHubCommunicationFactory(iotHubClientFactoryMethod);

            ServerBootstrap server = new ServerBootstrap()
                .Group(executorGroup)
                .Channel<TcpServerSocketChannel>()
                .ChildOption(ChannelOption.Allocator, bufAllocator)
                .ChildOption(ChannelOption.AutoRead, false)
                .ChildHandler(new ActionChannelInitializer<IChannel>(ch =>
                {
                    ch.Pipeline.AddLast(TlsHandler.Server(this.tlsCertificate));
                    ch.Pipeline.AddLast(
                        MqttEncoder.Instance,
                        new MqttDecoder(true, 256 * 1024),
                        new LoggingHandler(),
                        new MqttIotHubAdapter(
                            settings,
                            sessionStateProvider,
                            authProvider,
                            qos2StateProvider,
                            iotHubFactory,
                            topicNameRouter),
                        new XUnitLoggingHandler(this.output));
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

        IPAddress ServerAddress { get; set; }

        int ProtocolGatewayPort { get; }

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

        /// <summary>
        ///     Tests sequence of actions:
        ///     - CONNECT (incl. loading of session state, connection to IoT Hub)
        ///     - SUBSCRIBE (incl. saving changes to session state)
        ///     - PUBLISH QoS 0, QoS 1 from device (incl. verification through Event Hub on receiving end)
        ///     - PUBLISH QoS 0, QoS 1, QoS 2 messages to device
        ///     - DISCONNECT
        /// </summary>
        [Fact]
        public async Task BasicFunctionalityTest()
        {
            await this.EnsureServerInitializedAsync();

            int protocolGatewayPort = this.ProtocolGatewayPort;

            string iotHubConnectionString = ConfigurationManager.AppSettings["IoTHubConnectionString"];
            IotHubConnectionStringBuilder hubConnectionStringBuilder = IotHubConnectionStringBuilder.Create(iotHubConnectionString);
            bool deviceNameProvided;
            this.deviceId = ConfigurationManager.AppSettings["End2End.DeviceName"];
            if (!string.IsNullOrEmpty(this.deviceId))
            {
                deviceNameProvided = true;
            }
            else
            {
                deviceNameProvided = false;
                this.deviceId = Guid.NewGuid().ToString("N");
            }

            RegistryManager registryManager = RegistryManager.CreateFromConnectionString(iotHubConnectionString);
            if (!deviceNameProvided)
            {
                await registryManager.AddDeviceAsync(new Device(this.deviceId));
                this.ScheduleCleanup(async () =>
                {
                    await registryManager.RemoveDeviceAsync(this.deviceId);
                    await registryManager.CloseAsync();
                });
            }

            Device device = await registryManager.GetDeviceAsync(this.deviceId);
            this.deviceSas =
                new SharedAccessSignatureBuilder
                {
                    Key = device.Authentication.SymmetricKey.PrimaryKey,
                    Target = $"{hubConnectionStringBuilder.HostName}/devices/{this.deviceId}",
                    KeyName = null,
                    TimeToLive = TimeSpan.FromMinutes(20)
                }
                    .ToSignature();

            EventHubClient eventHubClient = EventHubClient.CreateFromConnectionString(iotHubConnectionString, "messages/events");
            EventHubConsumerGroup ehGroup = eventHubClient.GetDefaultConsumerGroup();

            string[] partitionIds = (await eventHubClient.GetRuntimeInformationAsync()).PartitionIds;
            EventHubReceiver[] receivers = await Task.WhenAll(partitionIds.Select(pid => ehGroup.CreateReceiverAsync(pid.Trim(), DateTime.UtcNow)));

            ServiceClient serviceClient = ServiceClient.CreateFromConnectionString(iotHubConnectionString);

            Stopwatch sw = Stopwatch.StartNew();

            var clientScenarios = new ClientScenarios(hubConnectionStringBuilder.HostName, this.deviceId, this.deviceSas);

            var group = new MultithreadEventLoopGroup();
            string targetHost = this.tlsCertificate.GetNameInfo(X509NameType.DnsName, false);

            var testPart1Promise = new TaskCompletionSource();
            Bootstrap bootstrap = new Bootstrap()
                .Group(group)
                .Channel<TcpSocketChannel>()
                .Option(ChannelOption.TcpNodelay, true)
                .Handler(this.ComposeClientChannelInitializer(targetHost, clientScenarios.GetPart1Steps, testPart1Promise));
            IChannel clientChannel = await bootstrap.ConnectAsync(this.ServerAddress, protocolGatewayPort);
            this.ScheduleCleanup(() => clientChannel.CloseAsync());

            Task testWorkTask = Task.Run(async () =>
            {
                Tuple<EventData, string>[] ehMessages = await CollectEventHubMessagesAsync(receivers, 2);
                Tuple<EventData, string> qos0Event = Assert.Single(ehMessages.Where(x => TelemetryQoS0Content.Equals(x.Item2, StringComparison.Ordinal)));
                Tuple<EventData, string> qos1Event = Assert.Single(ehMessages.Where(x => TelemetryQoS1Content.Equals(x.Item2, StringComparison.Ordinal)));

                string qosPropertyName = ConfigurationManager.AppSettings["QoSPropertyName"];

                var qos0Notification = new Message(Encoding.UTF8.GetBytes(NotificationQoS0Content));
                qos0Notification.Properties[qosPropertyName] = "0";
                qos0Notification.Properties["subTopic"] = "tips";
                await serviceClient.SendAsync(this.deviceId, qos0Notification);

                var qos1Notification = new Message(Encoding.UTF8.GetBytes(NotificationQoS1Content));
                qos1Notification.Properties["subTopic"] = "firmware-update";
                await serviceClient.SendAsync(this.deviceId, qos1Notification);

                var qos2Notification = new Message(Encoding.UTF8.GetBytes(NotificationQoS2Content));
                qos2Notification.Properties[qosPropertyName] = "2";
                qos2Notification.Properties["subTopic"] = "critical-alert";
                await serviceClient.SendAsync(this.deviceId, qos2Notification);

                var qos2Notification2 = new Message(Encoding.UTF8.GetBytes(NotificationQoS2Content2));
                qos2Notification2.Properties[qosPropertyName] = "2";
                await serviceClient.SendAsync(this.deviceId, qos2Notification2);
            });

            await Task.WhenAll(testPart1Promise.Task, testWorkTask).WithTimeout(TestTimeout);

            this.output.WriteLine($"part 1 completed in {sw.Elapsed}");
            await this.EnsureDeviceQueueLengthAsync(hubConnectionStringBuilder.HostName, device, 1);
            this.output.WriteLine($"part 1 clean completed in {sw.Elapsed}");

            string part2Payload = Guid.NewGuid().ToString("N");
            await serviceClient.SendAsync(this.deviceId, new Message(Encoding.ASCII.GetBytes(part2Payload)));

            var testPart2Promise = new TaskCompletionSource();
            IChannel clientChannelPart2 = await bootstrap
                .Handler(this.ComposeClientChannelInitializer(targetHost, func => clientScenarios.GetPart2Steps(func, part2Payload), testPart2Promise))
                .ConnectAsync(this.ServerAddress, protocolGatewayPort);
            this.ScheduleCleanup(async () =>
            {
                await clientChannelPart2.CloseAsync();
                await group.ShutdownGracefullyAsync();
            });

            await testPart2Promise.Task.WithTimeout(TestTimeout);

            this.output.WriteLine($"Core test completed in {sw.Elapsed}");

            await this.EnsureDeviceQueueLengthAsync(hubConnectionStringBuilder.HostName, device, 0);
        }

        async Task EnsureDeviceQueueLengthAsync(string hostname, Device device, int expectedLength)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));

            DeviceClient deviceClient = null;
            try
            {
                deviceClient = DeviceClient.Create(
                    hostname,
                    new DeviceAuthenticationWithRegistrySymmetricKey(this.deviceId, device.Authentication.SymmetricKey.PrimaryKey));
                int leftToTarget = expectedLength;
                while (true)
                {
                    using (Client.Message message = await deviceClient.ReceiveAsync(TimeSpan.FromSeconds(2)))
                    {
                        if (message == null)
                        {
                            break;
                        }
                        leftToTarget--;
                        Assert.True(leftToTarget >= 0, $"actual device queue length is greater than expected ({expectedLength}).");
                    }
                }
                Assert.True(leftToTarget == 0, $"actual device length is less than expected ({expectedLength}).");
            }
            finally
            {
                if (deviceClient != null)
                {
                    await deviceClient.CloseAsync();
                }
            }
        }

        ActionChannelInitializer<ISocketChannel> ComposeClientChannelInitializer(string targetHost, Func<Func<object>, IEnumerable<TestScenarioStep>> testScenarioProvider, TaskCompletionSource testPromise)
        {
            return new ActionChannelInitializer<ISocketChannel>(ch => ch.Pipeline.AddLast(
                TlsHandler.Client(targetHost, null, (sender, certificate, chain, errors) => true),
                MqttEncoder.Instance,
                new MqttDecoder(false, 256 * 1024),
                new LoggingHandler(),
                new TestScenarioRunner(testScenarioProvider, testPromise, CommunicationTimeout, CommunicationTimeout),
                new XUnitLoggingHandler(this.output)));
        }

        static async Task<Tuple<EventData, string>[]> CollectEventHubMessagesAsync(EventHubReceiver[] receivers, int messagesPending)
        {
            List<Task<EventData>> receiveTasks = receivers.Select(r => r.ReceiveAsync(TimeSpan.FromMinutes(20))).ToList();
            var ehMessages = new Tuple<EventData, string>[messagesPending];
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
            return ehMessages;
        }

        class ClientScenarios
        {
            readonly string iotHubName;
            readonly string clientId;
            readonly string password;

            public ClientScenarios(string iotHubName, string clientId, string password)
            {
                this.iotHubName = iotHubName;
                this.clientId = clientId;
                this.password = password;
            }

            public IEnumerable<TestScenarioStep> GetPart1Steps(Func<object> currentMessageFunc) => Flatten(this.GetPart1StepsInternal(currentMessageFunc));

            IEnumerable<IEnumerable<TestScenarioStep>> GetPart1StepsInternal(Func<object> currentMessageFunc)
            {
                yield return this.GetConnectSteps(currentMessageFunc);
                yield return this.GetSubscriptionSteps(currentMessageFunc);
                yield return this.GetPart1SpecificSteps(currentMessageFunc);
            }

            IEnumerable<TestScenarioStep> GetPart1SpecificSteps(Func<object> currentMessageFunc)
            {
                int publishQoS1PacketId = GetRandomPacketId();
                yield return TestScenarioStep.Write(
                    new PublishPacket(QualityOfService.AtMostOnce, false, false)
                    {
                        //TopicName = string.Format("devices/{0}/messages/log/verbose/", clientId),
                        TopicName = $"devices/{this.clientId}/messages/events",
                        Payload = Unpooled.WrappedBuffer(Encoding.UTF8.GetBytes("{\"test\": \"telemetry-QoS0\"}"))
                    },
                    new PublishPacket(QualityOfService.AtLeastOnce, false, false)
                    {
                        PacketId = publishQoS1PacketId,
                        TopicName = $"devices/{this.clientId}/messages/events",
                        Payload = Unpooled.WrappedBuffer(Encoding.UTF8.GetBytes("{\"test\": \"telemetry\"}"))
                    });

                var packets = new Packet[5];
                for (int i = packets.Length - 1; i >= 0; i--)
                {
                    packets[i] = Assert.IsAssignableFrom<Packet>(currentMessageFunc());
                    if (i > 0)
                    {
                        yield return TestScenarioStep.ReadMore();
                    }
                }

                PubAckPacket pubAckPacket = Assert.Single(packets.OfType<PubAckPacket>());
                Assert.Equal(publishQoS1PacketId, pubAckPacket.PacketId);

                PublishPacket publishQoS0Packet = Assert.Single(packets.OfType<PublishPacket>().Where(x => x.QualityOfService == QualityOfService.AtMostOnce));
                this.AssertPacketCoreValue(publishQoS0Packet, NotificationQoS0Content);

                PublishPacket publishQoS1Packet = Assert.Single(packets.OfType<PublishPacket>().Where(x => x.QualityOfService == QualityOfService.AtLeastOnce));
                this.AssertPacketCoreValue(publishQoS1Packet, NotificationQoS1Content);

                PublishPacket[] publishQoS2Packets = packets.OfType<PublishPacket>().Where(x => x.QualityOfService == QualityOfService.ExactlyOnce).Reverse().ToArray();
                Assert.Equal(2, publishQoS2Packets.Length);
                PublishPacket publishQoS2Packet1 = publishQoS2Packets[0];
                this.AssertPacketCoreValue(publishQoS2Packet1, NotificationQoS2Content);
                PublishPacket publishQoS2Packet2 = publishQoS2Packets[1];
                this.AssertPacketCoreValue(publishQoS2Packet2, NotificationQoS2Content2);

                yield return TestScenarioStep.Write(
                    PubAckPacket.InResponseTo(publishQoS1Packet),
                    PubRecPacket.InResponseTo(publishQoS2Packet1),
                    PubRecPacket.InResponseTo(publishQoS2Packet2));

                var pubRelQoS2Packet1 = Assert.IsAssignableFrom<PubRelPacket>(currentMessageFunc());
                Assert.Equal(publishQoS2Packet1.PacketId, pubRelQoS2Packet1.PacketId);

                yield return TestScenarioStep.ReadMore();

                var pubRelQoS2Packet2 = Assert.IsAssignableFrom<PubRelPacket>(currentMessageFunc());
                Assert.Equal(publishQoS2Packet2.PacketId, pubRelQoS2Packet2.PacketId);

                yield return TestScenarioStep.Write(
                    false, // it is a final step and we do not expect response
                    PubCompPacket.InResponseTo(pubRelQoS2Packet1),
                    DisconnectPacket.Instance);

                // device queue still contains QoS 2 packet 2 which was PUBRECed but not PUBCOMPed.
            }

            public IEnumerable<TestScenarioStep> GetPart2Steps(Func<object> currentMessageFunc, string expectedPayload) => Flatten(this.GetPart2StepsInternal(currentMessageFunc));

            IEnumerable<IEnumerable<TestScenarioStep>> GetPart2StepsInternal(Func<object> currentMessageFunc)
            {
                yield return this.GetConnectSteps(currentMessageFunc);
                yield return this.GetPart2SpecificSteps(currentMessageFunc);
            }

            IEnumerable<TestScenarioStep> GetPart2SpecificSteps(Func<object> currentMessageFunc)
            {
                yield return TestScenarioStep.ReadMore();

                var packets = new Packet[2];
                for (int i = packets.Length - 1; i >= 0; i--)
                {
                    packets[i] = Assert.IsAssignableFrom<Packet>(currentMessageFunc());
                    if (i > 0)
                    {
                        yield return TestScenarioStep.ReadMore();
                    }
                }

                PublishPacket qos1Packet = Assert.Single(packets.OfType<PublishPacket>());

                Assert.Equal(QualityOfService.AtLeastOnce, qos1Packet.QualityOfService);
                this.AssertPacketCoreValue(qos1Packet, Encoding.ASCII.GetString(qos1Packet.Payload.ToArray()));

                PubRelPacket pubRelQos2Packet = Assert.Single(packets.OfType<PubRelPacket>());

                yield return TestScenarioStep.Write(
                    false,
                    PubAckPacket.InResponseTo(qos1Packet),
                    PubCompPacket.InResponseTo(pubRelQos2Packet),
                    DisconnectPacket.Instance);
            }

            void AssertPacketCoreValue(PublishPacket packet, string expectedPayload)
            {
                Assert.Equal($"devices/{this.clientId}/messages/devicebound", packet.TopicName);
                Assert.Equal(expectedPayload, Encoding.UTF8.GetString(packet.Payload.ToArray()));
            }

            IEnumerable<TestScenarioStep> GetConnectSteps(Func<object> currentMessageFunc)
            {
                yield return TestScenarioStep.Write(new ConnectPacket
                {
                    ClientId = this.clientId,
                    HasUsername = true,
                    Username = this.iotHubName + "/" + this.clientId,
                    HasPassword = true,
                    Password = this.password,
                    KeepAliveInSeconds = 120,
                    HasWill = true,
                    WillTopicName = "last/word",
                    WillMessage = Unpooled.WrappedBuffer(Encoding.UTF8.GetBytes("oops"))
                });

                var connAckPacket = Assert.IsType<ConnAckPacket>(currentMessageFunc());
                Assert.Equal(ConnectReturnCode.Accepted, connAckPacket.ReturnCode);
            }

            IEnumerable<TestScenarioStep> GetSubscriptionSteps(Func<object> currentMessageFunc)
            {
                int subscribePacket1Id = GetRandomPacketId();
                int subscribePacket2Id = GetRandomPacketId();
                yield return TestScenarioStep.Write(
                    new SubscribePacket(subscribePacket1Id, new SubscriptionRequest($"devices/{this.clientId}/messages/devicebound/#", QualityOfService.ExactlyOnce)),
                    new SubscribePacket(subscribePacket2Id, new SubscriptionRequest("multi/subscribe/check", QualityOfService.AtMostOnce)));

                var subAck1Packet = Assert.IsType<SubAckPacket>(currentMessageFunc());
                Assert.Equal(subscribePacket1Id, subAck1Packet.PacketId);
                Assert.Equal(1, subAck1Packet.ReturnCodes.Count);
                Assert.Equal(QualityOfService.ExactlyOnce, subAck1Packet.ReturnCodes[0]);

                yield return TestScenarioStep.ReadMore();

                var subAck2Packet = Assert.IsType<SubAckPacket>(currentMessageFunc());
                Assert.Equal(subscribePacket2Id, subAck2Packet.PacketId);
                Assert.Equal(1, subAck2Packet.ReturnCodes.Count);
                Assert.Equal(QualityOfService.AtMostOnce, subAck2Packet.ReturnCodes[0]);
            }

            static int GetRandomPacketId() => Guid.NewGuid().GetHashCode() & ushort.MaxValue;

            static IEnumerable<T> Flatten<T>(IEnumerable<IEnumerable<T>> source) => source.SelectMany(_ => _);
        }
    }
}