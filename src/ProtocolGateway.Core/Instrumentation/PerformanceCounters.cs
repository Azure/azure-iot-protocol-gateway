// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Instrumentation
{
#if !NETSTANDARD1_3
    using System.Diagnostics;
    using System.Collections.Generic;
#endif

    public static class PerformanceCounters
    {
        const string CategoryName = "Azure IoT protocol gateway";
        const string CategoryHelp = "CategoryHelp";
        const string ConnectionsEstablishedTotalCounterName = "Connections Established Total";
        const string ConnectionsCurrentCounterName = "Connections Current";
        const string ConnectionsEstablishedPerSecondCounterName = "Connections Established/sec";
        const string ConnectionFailedAuthPerSecondCounterName = "Failed Connections (due to Auth Issues)/sec";
        const string ConnectionFailedOperationalPerSecondCounterName = "Failed Connections (due to Operational Errors)/sec";
        const string PacketsReceivedPerSecondCounterName = "MQTT Packets Received/sec";
        const string PacketsSentPerSecondCounterName = "MQTT Packets Sent/sec";
        const string PublishPacketsReceivedPerSecondCounterName = "MQTT PUBLISH packets Received/sec";
        const string PublishPacketsSentPerSecondCounterName = "MQTT PUBLISH packets Sent/sec";

        const string MessagesReceivedPerSecondCounterName = "Messages Received/sec";
        const string MessagesSentPerSecondCounterName = "Messages Sent/sec";
        const string MessagesRejectedPerSecondCounterName = "Messages Rejected/sec";
        const string OutboundMessageProcessingTimeCounterName = "Outbound Message Processing Time, msec"; // from received to completed (incl roundtrip to ack)
        const string OutboundMessageProcessingTimeBaseCounterName = "Outbound Message Processing Time Base";
        const string InboundMessageProcessingTimeCounterName = "Inbound Message Processing Time, msec"; // from received to completed (incl roundtrip to ack)
        const string InboundMessageProcessingTimeBaseCounterName = "Inbound Message Processing Time Base";

        const string TotalMethodsInvokedCounterName = "Methods Invoked Total";
        const string MethodsInvokedPerSecondCounterName = "Methods Invoked Per Second";
        const string TotalCommandsReceivedCounterName = "Commands Received Total";
        const string CommandsReceivedPerSecondCounterName = "Commands Received Per Second";

        static readonly IPerformanceCounterManager ManagerInstance = GetPerformanceCounterManager();
        public static readonly IPerformanceCounter ConnectionsEstablishedTotal = ManagerInstance.GetCounter(CategoryName, ConnectionsEstablishedTotalCounterName);
        public static readonly IPerformanceCounter ConnectionsCurrent = ManagerInstance.GetCounter(CategoryName, ConnectionsCurrentCounterName);
        public static readonly IPerformanceCounter ConnectionsEstablishedPerSecond = ManagerInstance.GetCounter(CategoryName, ConnectionsEstablishedPerSecondCounterName);
        public static readonly IPerformanceCounter ConnectionFailedAuthPerSecond = ManagerInstance.GetCounter(CategoryName, ConnectionFailedAuthPerSecondCounterName);
        public static readonly IPerformanceCounter ConnectionFailedOperationalPerSecond = ManagerInstance.GetCounter(CategoryName, ConnectionFailedOperationalPerSecondCounterName);
        public static readonly IPerformanceCounter PacketsReceivedPerSecond = ManagerInstance.GetCounter(CategoryName, PacketsReceivedPerSecondCounterName);
        public static readonly IPerformanceCounter PacketsSentPerSecond = ManagerInstance.GetCounter(CategoryName, PacketsSentPerSecondCounterName);
        public static readonly IPerformanceCounter PublishPacketsReceivedPerSecond = ManagerInstance.GetCounter(CategoryName, PublishPacketsReceivedPerSecondCounterName);
        public static readonly IPerformanceCounter PublishPacketsSentPerSecond = ManagerInstance.GetCounter(CategoryName, PublishPacketsSentPerSecondCounterName);
        public static readonly IPerformanceCounter MessagesReceivedPerSecond = ManagerInstance.GetCounter(CategoryName, MessagesReceivedPerSecondCounterName);
        public static readonly IPerformanceCounter MessagesRejectedPerSecond = ManagerInstance.GetCounter(CategoryName, MessagesRejectedPerSecondCounterName);
        public static readonly IPerformanceCounter MessagesSentPerSecond = ManagerInstance.GetCounter(CategoryName, MessagesSentPerSecondCounterName);
        public static readonly IPerformanceCounter TotalMethodsInvoked = ManagerInstance.GetCounter(CategoryName, TotalMethodsInvokedCounterName);
        public static readonly IPerformanceCounter MethodsInvokedPerSecond = ManagerInstance.GetCounter(CategoryName, MethodsInvokedPerSecondCounterName);
        public static readonly IPerformanceCounter TotalCommandsReceived = ManagerInstance.GetCounter(CategoryName, TotalCommandsReceivedCounterName);
        public static readonly IPerformanceCounter CommandsReceivedPerSecond = ManagerInstance.GetCounter(CategoryName, CommandsReceivedPerSecondCounterName);

        public static readonly AveragePerformanceCounter OutboundMessageProcessingTime = new AveragePerformanceCounter(
           ManagerInstance.GetCounter(CategoryName, OutboundMessageProcessingTimeCounterName),
           ManagerInstance.GetCounter(CategoryName, OutboundMessageProcessingTimeBaseCounterName));

        public static readonly AveragePerformanceCounter InboundMessageProcessingTime = new AveragePerformanceCounter(
            ManagerInstance.GetCounter(CategoryName, InboundMessageProcessingTimeCounterName),
            ManagerInstance.GetCounter(CategoryName, InboundMessageProcessingTimeBaseCounterName));

        public static void RegisterCountersIfRequired()
        {
            var manager = GetPerformanceCounterManager();
        }

        private static IPerformanceCounterManager GetPerformanceCounterManager()
        {
#if NETSTANDARD1_3
            return new EmptyPerformanceCounterManager();
#else
            return new Manager();
#endif

        }

#if !NETSTANDARD1_3
        class Manager : WindowsPerformanceCounterManager
        {
            public Manager() : base(new Dictionary<PerformanceCounterCategoryInfo, CounterCreationData[]>
                {
                    {
                        new PerformanceCounterCategoryInfo(CategoryName, PerformanceCounterCategoryType.SingleInstance, CategoryHelp),
                        new[]
                        {
                            new CounterCreationData(ConnectionsEstablishedTotalCounterName, "", PerformanceCounterType.NumberOfItems64),
                            new CounterCreationData(ConnectionsCurrentCounterName, "", PerformanceCounterType.NumberOfItems64),
                            new CounterCreationData(ConnectionsEstablishedPerSecondCounterName, "", PerformanceCounterType.RateOfCountsPerSecond64),
                            new CounterCreationData(ConnectionFailedAuthPerSecondCounterName, "", PerformanceCounterType.RateOfCountsPerSecond64),
                            new CounterCreationData(ConnectionFailedOperationalPerSecondCounterName, "", PerformanceCounterType.RateOfCountsPerSecond64),
                            new CounterCreationData(PacketsReceivedPerSecondCounterName, "", PerformanceCounterType.RateOfCountsPerSecond64),
                            new CounterCreationData(PacketsSentPerSecondCounterName, "", PerformanceCounterType.RateOfCountsPerSecond64),
                            new CounterCreationData(PublishPacketsReceivedPerSecondCounterName, "", PerformanceCounterType.RateOfCountsPerSecond32),
                            new CounterCreationData(PublishPacketsSentPerSecondCounterName, "", PerformanceCounterType.RateOfCountsPerSecond32),
                            new CounterCreationData(MessagesReceivedPerSecondCounterName, "", PerformanceCounterType.RateOfCountsPerSecond32),
                            new CounterCreationData(MessagesRejectedPerSecondCounterName, "", PerformanceCounterType.RateOfCountsPerSecond32),
                            new CounterCreationData(MessagesSentPerSecondCounterName, "", PerformanceCounterType.RateOfCountsPerSecond32),
                            new CounterCreationData(OutboundMessageProcessingTimeCounterName, "", PerformanceCounterType.AverageCount64),
                            new CounterCreationData(OutboundMessageProcessingTimeBaseCounterName, "", PerformanceCounterType.AverageBase),
                            new CounterCreationData(InboundMessageProcessingTimeCounterName, "", PerformanceCounterType.AverageCount64),
                            new CounterCreationData(InboundMessageProcessingTimeBaseCounterName, "", PerformanceCounterType.AverageBase),
                            new CounterCreationData(TotalMethodsInvokedCounterName, "", PerformanceCounterType.NumberOfItems64),
                            new CounterCreationData(MethodsInvokedPerSecondCounterName, "", PerformanceCounterType.RateOfCountsPerSecond64),
                            new CounterCreationData(TotalCommandsReceivedCounterName, "", PerformanceCounterType.NumberOfItems64),
                            new CounterCreationData(CommandsReceivedPerSecondCounterName, "", PerformanceCounterType.RateOfCountsPerSecond64),
                        }
                    }
                })
            {
            }
        }
#endif
    }
}