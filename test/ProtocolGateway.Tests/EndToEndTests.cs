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
    using DotNetty.Handlers.Tls;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using global::ProtocolGateway.Samples.Common;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Common.Security;
    using Microsoft.Azure.Devices.Gateway.Tests;
    using Microsoft.Azure.Devices.ProtocolGateway;
    using Microsoft.Azure.Devices.ProtocolGateway.Instrumentation;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Auth;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Routing;
    using Microsoft.Azure.Devices.ProtocolGateway.Providers.CloudStorage;
    using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;
    using Microsoft.ServiceBus.Messaging;
    using Xunit;
    using Xunit.Abstractions;
    using IotHubConnectionStringBuilder = Microsoft.Azure.Devices.IotHubConnectionStringBuilder;
    using Message = Microsoft.Azure.Devices.Message;
    using TaskExtensions = Microsoft.Azure.Devices.Gateway.Tests.Extensions.TaskExtensions;

    public class EndToEndTests : IDisposable
    {
        const string TelemetryQoS0Content = "{\"test\": \"telemetry-QoS0\"}";
        const string TelemetryQoS1Content = "{\"test\": \"telemetry\"}";
        const string NotificationQoS0Content = "{\"test\": \"fire and forget\"}";
        const string NotificationQoS1Content = "{\"test\": \"notify (at least once)\"}";
        const string NotificationQoS2Content = "{\"test\": \"exactly once\"}";

        readonly ITestOutputHelper output;
        readonly ISettingsProvider settingsProvider;
        Func<Task> cleanupFunc;
        readonly ObservableEventListener eventListener;
        string deviceId;
        string deviceSas;
        readonly X509Certificate2 tlsCertificate;
        static readonly TimeSpan CommunicationTimeout = TimeSpan.FromMilliseconds(-1); //TimeSpan.FromSeconds(15);
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
            var authProvider = new SasTokenAuthenticationProvider();
            var topicNameRouter = new TopicNameRouter();

            DeviceClientFactoryFunc deviceClientFactoryFactory = IotHubDeviceClient.PreparePoolFactory(settings.IotHubConnectionString + ";DeviceId=stub", "a", 1);

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
                        new MqttIotHubAdapter(
                            settings,
                            deviceClientFactoryFactory,
                            sessionStateProvider,
                            authProvider,
                            topicNameRouter,
                            qos2StateProvider),
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
                    Target = string.Format("{0}/devices/{1}", hubConnectionStringBuilder.HostName, this.deviceId),
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

            var testPromise = new TaskCompletionSource();

            var group = new MultithreadEventLoopGroup();
            string targetHost = this.tlsCertificate.GetNameInfo(X509NameType.DnsName, false);
            Bootstrap bootstrap = new Bootstrap()
                .Group(group)
                .Channel<TcpSocketChannel>()
                .Option(ChannelOption.TcpNodelay, true)
                .Handler(new ActionChannelInitializer<ISocketChannel>(ch => ch.Pipeline.AddLast(
                    TlsHandler.Client(targetHost, null, (sender, certificate, chain, errors) => true),
                    MqttEncoder.Instance,
                    new MqttDecoder(false, 256 * 1024),
                    new TestScenarioRunner(cmf => GetClientScenario(cmf, hubConnectionStringBuilder.HostName, this.deviceId, this.deviceSas), testPromise, CommunicationTimeout, CommunicationTimeout),
                    new XUnitLoggingHandler(this.output))));
            IChannel clientChannel = await bootstrap.ConnectAsync(this.ServerAddress, protocolGatewayPort);
            this.ScheduleCleanup(async () =>
            {
                await clientChannel.CloseAsync();
                await group.ShutdownGracefullyAsync();
            });

            Task testWorkTask = Task.Run(async () =>
            {
                Tuple<EventData, string>[] ehMessages = await CollectEventHubMessagesAsync(receivers, 2);

                Tuple<EventData, string> qos0Event = Assert.Single(ehMessages.Where(x => TelemetryQoS0Content.Equals(x.Item2, StringComparison.Ordinal)));
                //Assert.Equal((string)qos0Event.Item1.Properties["level"], "verbose");
                //Assert.Equal((string)qos0Event.Item1.Properties["subject"], "n/a");

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
            });

            Task timeoutTask = Task.Run(async () =>
            {
                await Task.Delay(TestTimeout);
                throw new TimeoutException("Test has timed out.");
            });

            await TaskExtensions.WhenSome(new[]
            {
                testPromise.Task,
                testWorkTask,
                timeoutTask
            }, 2);

            this.output.WriteLine(string.Format("Core test completed in {0}", sw.Elapsed));

            // making sure that device queue is empty.
            await Task.Delay(TimeSpan.FromSeconds(3));

            DeviceClient deviceClient = DeviceClient.Create(
                hubConnectionStringBuilder.HostName,
                new DeviceAuthenticationWithRegistrySymmetricKey(this.deviceId, device.Authentication.SymmetricKey.PrimaryKey));
            Client.Message message = await deviceClient.ReceiveAsync(TimeSpan.FromSeconds(1));
            Assert.True(message == null, string.Format("Message is not null: {0}", message));
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

        static IEnumerable<TestScenarioStep> GetClientScenario(Func<object> currentMessageFunc, string iotHubName, string clientId, string password)
        {
            yield return TestScenarioStep.Write(new ConnectPacket
            {
                ClientId = clientId,
                HasUsername = true,
                Username = iotHubName + "/" + clientId,
                HasPassword = true,
                Password = password,
                KeepAliveInSeconds = 120,
                HasWill = true,
                WillTopicName = "last/word",
                WillMessage = Unpooled.WrappedBuffer(Encoding.UTF8.GetBytes("oops"))
            });

            var connAckPacket = Assert.IsType<ConnAckPacket>(currentMessageFunc());
            Assert.Equal(ConnectReturnCode.Accepted, connAckPacket.ReturnCode);

            int subscribePacketId = GetRandomPacketId();
            yield return TestScenarioStep.Write(new SubscribePacket
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
            Assert.Equal(QualityOfService.ExactlyOnce, subAckPacket.ReturnCodes[0]);

            int publishQoS1PacketId = GetRandomPacketId();
            yield return TestScenarioStep.Write(
                new PublishPacket(QualityOfService.AtMostOnce, false, false)
                {
                    //TopicName = string.Format("devices/{0}/messages/log/verbose/", clientId),
                    TopicName = string.Format("devices/{0}/messages/events", clientId),
                    Payload = Unpooled.WrappedBuffer(Encoding.UTF8.GetBytes("{\"test\": \"telemetry-QoS0\"}"))
                },
                new PublishPacket(QualityOfService.AtLeastOnce, false, false)
                {
                    PacketId = publishQoS1PacketId,
                    TopicName = string.Format("devices/{0}/messages/events", clientId),
                    Payload = Unpooled.WrappedBuffer(Encoding.UTF8.GetBytes("{\"test\": \"telemetry\"}"))
                });

            var packets = new Packet[4];
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
            //Assert.Equal(string.Format("devices/{0}/messages/devicebound/tips", clientId), publishQoS0Packet.TopicName);
            Assert.Equal(string.Format("devices/{0}/messages/devicebound", clientId), publishQoS0Packet.TopicName);
            Assert.Equal(NotificationQoS0Content, Encoding.UTF8.GetString(publishQoS0Packet.Payload.ToArray()));

            PublishPacket publishQoS1Packet = Assert.Single(packets.OfType<PublishPacket>().Where(x => x.QualityOfService == QualityOfService.AtLeastOnce));
            //Assert.Equal(string.Format("devices/{0}/messages/devicebound/firmware-update", clientId), publishQoS1Packet.TopicName);
            Assert.Equal(string.Format("devices/{0}/messages/devicebound", clientId), publishQoS1Packet.TopicName);
            Assert.Equal(NotificationQoS1Content, Encoding.UTF8.GetString(publishQoS1Packet.Payload.ToArray()));

            PublishPacket publishQoS2Packet = Assert.Single(packets.OfType<PublishPacket>().Where(x => x.QualityOfService == QualityOfService.ExactlyOnce));
            //Assert.Equal(string.Format("devices/{0}/messages/devicebound/critical-alert", clientId), publishQoS2Packet.TopicName);
            Assert.Equal(string.Format("devices/{0}/messages/devicebound", clientId), publishQoS2Packet.TopicName);
            Assert.Equal(NotificationQoS2Content, Encoding.UTF8.GetString(publishQoS2Packet.Payload.ToArray()));

            yield return TestScenarioStep.Write(
                PubAckPacket.InResponseTo(publishQoS1Packet),
                PubRecPacket.InResponseTo(publishQoS2Packet));

            var pubRelQoS2Packet = Assert.IsAssignableFrom<PubRelPacket>(currentMessageFunc());
            Assert.Equal(publishQoS2Packet.PacketId, pubRelQoS2Packet.PacketId);

            yield return TestScenarioStep.Write(
                false, // it is a final step and we do not expect response
                PubCompPacket.InResponseTo(pubRelQoS2Packet),
                DisconnectPacket.Instance);
        }

        static int GetRandomPacketId()
        {
            return Guid.NewGuid().GetHashCode() & ushort.MaxValue;
        }
    }
}