// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.Gateway.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Mqtt;
    using DotNetty.Handlers.Tls;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using Microsoft.Azure.Devices.Gateway.Cloud;
    using Microsoft.Azure.Devices.Gateway.Core;
    using Microsoft.Azure.Devices.Gateway.Core.Mqtt;
    using Microsoft.ServiceBus.Messaging;
    using Xunit;
    using Xunit.Abstractions;

    public class EndToEndTests : IDisposable
    {
        readonly ITestOutputHelper output;
        readonly ISettingsProvider settingsProvider;
        Func<Task> cleanupFunc;

        public EndToEndTests(ITestOutputHelper output)
        {
            this.output = output;
            this.settingsProvider = new DefaultSettingsProvider();

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
            var bufAllocator = new PooledByteBufferAllocator(16 * 1024, 300 * 1024 * 1024 / threadCount); // reserve 100 MB for 64 KB buffers
            BlobSessionStateManager sessionStateManager = await BlobSessionStateManager.CreateAsync(this.settingsProvider.GetSetting("SessionStateManager.StorageConnectionString"), this.settingsProvider.GetSetting("SessionStateManager.StorageContainerName"));
            var certificate = new X509Certificate2("tlscert.pfx", "password");
            var settings = new Settings(this.settingsProvider);
            var authProvider = new StubAuthenticationProvider();

            ServerBootstrap server = new ServerBootstrap()
                .Group(executorGroup)
                .Channel<TcpServerSocketChannel>()
                .ChildOption(ChannelOption.Allocator, bufAllocator)
                .ChildOption(ChannelOption.AutoRead, false)
                .ChildHandler(new ActionChannelInitializer<IChannel>(ch =>
                {
                    ch.Pipeline.AddLast(new TlsHandler(certificate));
                    ch.Pipeline.AddLast(
                        ServerEncoder.Instance,
                        new ServerDecoder(256 * 1024),
                        new BridgeDriver(settings, sessionStateManager, authProvider));
                }));

            IChannel serverChannel = await server.BindAsync(IPAddress.Any, this.ProtocolGatewayPort);

            this.cleanupFunc = async () =>
            {
                await serverChannel.CloseAsync();
                await executorGroup.ShutdownGracefullyAsync();
            };
            this.ServerAddress = IPAddress.Loopback;
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
        }

        [Fact(Skip = "just a handy method to calc remaining length for MQTT packet")]
        public void CalcMqttRemainingLength()
        {
            int X = 10;
            do
            {
                int encodedByte = X % 128;
                X = X / 128;
                // if there are more data to encode, set the top bit of this byte 286
                if (X > 0)
                {
                    encodedByte = encodedByte | 128;
                }
                Console.WriteLine(encodedByte.ToString("X"));
            }
            while (X > 0);
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
            var clientMessages = new[]
            {
                new TcpScenarioMessage(1, "connect", true, 0, content: "10 10 00 04 4D 51 54 54 04 00 00 78 00 04 63 61 72 31"),
                new TcpScenarioMessage(2, "connack", false, 0, omit: new[] { new[] { 3, 0 } }, content: "20 02 00 00"),
                new TcpScenarioMessage(3, "subscribe", true, 0, content: "82 1B 00 01 00 16 6D 65 73 73 61 67 65 73 2F 64 65 76 69 63 65 62 6F 75 6E 64 2F 23 01"),
                new TcpScenarioMessage(4, "suback", false, 0, content: "90 03 00 01 01"),
                new TcpScenarioMessage(5, "publish QoS 0", true, 0, content: "30 2B 00 0F 6D 65 73 73 61 67 65 73 2F 65 76 65 6E 74 73 7B 22 74 65 73 74 22 3A 20 22 74 65 6C 65 6D 65 74 72 79 2D 51 6F 53 30 22 7D"),
                new TcpScenarioMessage(6, "publish QoS 1", true, 0, content: "32 28 00 0F 6D 65 73 73 61 67 65 73 2F 65 76 65 6E 74 73 00 02 7B 22 74 65 73 74 22 3A 20 22 74 65 6C 65 6D 65 74 72 79 22 7D"),
                new TcpScenarioMessage(7, "puback", false, 0, content: "40 02 00 02"),
                new TcpScenarioMessage(8, "publish QoS 0", false, 0, content: "30 36 00 19 6D 65 73 73 61 67 65 73 2F 64 65 76 69 63 65 62 6F 75 6E 64 2F 74 69 70 73 7B 22 74 65 73 74 22 3A 20 22 66 69 72 65 20 61 6E 64 20 66 6F 72 67 65 74 22 7D"),
                new TcpScenarioMessage(8, "publish QoS 1", false, 0, content: "32 4A 00 24 6D 65 73 73 61 67 65 73 2F 64 65 76 69 63 65 62 6F 75 6E 64 2F 66 69 72 6D 77 61 72 65 2D 75 70 64 61 74 65 00 01 7B 22 74 65 73 74 22 3A 20 22 6E 6F 74 69 66 79 20 28 61 74 20 6C 65 61 73 74 20 6F 6E 63 65 29 22 7D"),
                new TcpScenarioMessage(9, "puback", true, 0, content: "40 02 00 01"),
                new TcpScenarioMessage(10, "disconnect", true, 0, content: "E0 00")
            };

            int protocolGatewayPort = this.ProtocolGatewayPort;

            string deviceId = ConfigurationManager.AppSettings["End2End.DeviceName"];
            DeviceClient deviceClient = DeviceClient.CreateFromConnectionString(ConfigurationManager.AppSettings["IoTHub.ConnectionString"], deviceId);

            Message staleMessage;
            while ((staleMessage = await deviceClient.ReceiveAsync(TimeSpan.FromSeconds(1))) != null)
            {
                await deviceClient.CompleteAsync(staleMessage);
            }

            EventHubClient eventHubClient = EventHubClient.CreateFromConnectionString(ConfigurationManager.AppSettings["EventHub.ConnectionString"]);
            EventHubConsumerGroup ehGroup = eventHubClient.GetDefaultConsumerGroup();
            string[] partitionIds = ConfigurationManager.AppSettings["EventHub.Partitions"].Split(',');
            EventHubReceiver[] receivers = await Task.WhenAll(partitionIds.Select(pid => ehGroup.CreateReceiverAsync(pid.Trim(), DateTime.UtcNow)));

            ServiceClient serviceClient = ServiceClient.CreateFromConnectionString(ConfigurationManager.AppSettings["IoTHub.ConnectionString"]);

            var sw = Stopwatch.StartNew();
            var scenarioOptions = new TcpScenarioOptions
            {
                Address = this.ServerAddress,
                Port = protocolGatewayPort,
                Tls = true
            };
            Task deviceScenarioTask = new TcpScenarioRunner().RunAsync(scenarioOptions, clientMessages, CancellationToken.None);

            bool qos0Received = false;
            bool qos1Received = false;

            List<Task<EventData>> receiveTasks = receivers.Select(r => r.ReceiveAsync(TimeSpan.FromMinutes(20))).ToList();
            Task<EventData> receivedTask = await Task.WhenAny(receiveTasks);
            byte[] bytes = receivedTask.Result.GetBytes();
            string contentString = Encoding.UTF8.GetString(bytes);
            qos0Received |= TelemetryQoS0Content.Equals(contentString, StringComparison.Ordinal);
            qos1Received |= TelemetryQoS1Content.Equals(contentString, StringComparison.Ordinal);

            int receivedIndex = receiveTasks.IndexOf(receivedTask);
            receiveTasks[receivedIndex] = receivers[receivedIndex].ReceiveAsync(TimeSpan.FromMinutes(20));
            receivedTask = await Task.WhenAny(receiveTasks);
            bytes = receivedTask.Result.GetBytes();
            contentString = Encoding.UTF8.GetString(bytes);
            qos0Received |= TelemetryQoS0Content.Equals(contentString, StringComparison.Ordinal);
            qos1Received |= TelemetryQoS1Content.Equals(contentString, StringComparison.Ordinal);

            Assert.True(qos0Received, "QoS 0 message was not received or content did not match.");
            Assert.True(qos1Received, "QoS 1 message was not received or content did not match.");

            var qos0Notification = new Message(Encoding.UTF8.GetBytes(NotificationQoS0Content));
            qos0Notification.Properties[ConfigurationManager.AppSettings["IoTHub.QoSProperty"]] = "0";
            qos0Notification.Properties[ConfigurationManager.AppSettings["IoTHub.TopicNameProperty"]] = "messages/devicebound/tips";
            await serviceClient.SendAsync(deviceId, qos0Notification);
            var qos1Notification = new Message(Encoding.UTF8.GetBytes(NotificationQoS1Content));
            qos1Notification.Properties[ConfigurationManager.AppSettings["IoTHub.TopicNameProperty"]] = "messages/devicebound/firmware-update";
            await serviceClient.SendAsync(deviceId, qos1Notification);

            await deviceScenarioTask;

            this.output.WriteLine(string.Format("Core test completed in {0}", sw.Elapsed));

            // making sure that device queue is empty.
            await Task.Delay(TimeSpan.FromSeconds(3));
            Assert.Null(await deviceClient.ReceiveAsync(TimeSpan.FromSeconds(1)));
        }
    }
}