// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Providers.ReliableStorage
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Diagnostics.Tracing;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.ProtocolGateway.Identity;
    using Microsoft.Azure.Devices.ProtocolGateway.Instrumentation;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence;
    using Microsoft.ServiceFabric.Services.Client;
    using Microsoft.ServiceFabric.Services.Remoting.Client;

    using Murmur;

    public class ReliableQos2StatePersistenceProvider : IQos2StatePersistenceProvider
    {
        /// <summary>
        /// The partition count.
        /// </summary>
        const int PartitionCount = 0x10; // WARNING: changing partition count will change placement of device in partitions. Do it only if is wiped or migrated along the change.

        static readonly Uri BackEndEndpoint = new Uri("fabric:/ProtocolGateway.Host.Fabric/BackEnd");

        /// <summary>
        /// The partition keys.
        /// </summary>
        static readonly string[] PartitionKeys = Enumerable.Range(0, PartitionCount).Select(i => $"BackEnd_{i}").ToArray(); // WARNING: Do not change naming convention

        public ReliableQos2StatePersistenceProvider()
        {
        }

        public IQos2MessageDeliveryState Create(ulong sequenceNumber)
        {
            return new ReliableMessageDeliveryState(sequenceNumber);
        }

        public async Task<IQos2MessageDeliveryState> GetMessageAsync(IDeviceIdentity deviceIdentity, int packetId)
        {
            var partitionKey = CalculatePartitionKey(deviceIdentity.Id);
            if (CommonEventSource.Log.IsVerboseEnabled)
            {
                CommonEventSource.Log.Verbose($"Selecting partition {partitionKey} of SF reliable state for storing Device {deviceIdentity.Id} MQTT OoS2 message state", string.Empty); 
            }
            var servicePartitionKey = new ServicePartitionKey(partitionKey);
            var backEndService = ServiceProxy.Create<IBackEndService>(BackEndEndpoint, servicePartitionKey);
            return await backEndService.GetMessageAsync(deviceIdentity.Id, packetId).ConfigureAwait(false);
        }

        public Task DeleteMessageAsync(IDeviceIdentity deviceIdentity, int packetId, IQos2MessageDeliveryState message)
        {
            Contract.Requires(message != null);

            var reliableMessage = ValidateMessageType(message);
            var partitionKey = CalculatePartitionKey(deviceIdentity.Id);
            if (CommonEventSource.Log.IsVerboseEnabled)
            {
                CommonEventSource.Log.Verbose($"Selecting partition {partitionKey} of SF reliable state for storing Device {deviceIdentity.Id} MQTT OoS2 message state", string.Empty);
            }
            var servicePartitionKey = new ServicePartitionKey(partitionKey);
            var backEndService = ServiceProxy.Create<IBackEndService>(BackEndEndpoint, servicePartitionKey);
            return backEndService.DeleteMessageAsync(deviceIdentity.Id, packetId);
        }

        public Task SetMessageAsync(IDeviceIdentity deviceIdentity, int packetId, IQos2MessageDeliveryState message)
        {
            Contract.Requires(message != null);

            var reliableMessage = ValidateMessageType(message);
            var partitionKey = CalculatePartitionKey(deviceIdentity.Id);
            if (CommonEventSource.Log.IsVerboseEnabled)
            {
                CommonEventSource.Log.Verbose($"Selecting partition {partitionKey} of SF reliable state for storing Device {deviceIdentity.Id} MQTT OoS2 message state", string.Empty);
            }
            var servicePartitionKey = new ServicePartitionKey(partitionKey);
            var backEndService = ServiceProxy.Create<IBackEndService>(BackEndEndpoint, servicePartitionKey);
            return backEndService.SetMessageAsync(deviceIdentity.Id, packetId, reliableMessage);
        }

        static ReliableMessageDeliveryState ValidateMessageType(IQos2MessageDeliveryState message)
        {
            var reliableMessage = message as ReliableMessageDeliveryState;
            if (reliableMessage == null)
            {
                throw new ArgumentException(string.Format("Message is of unexpected type `{0}`. Only messages created through {1}.Create() method are supported",
                    message.GetType().Name, typeof(ReliableQos2StatePersistenceProvider).Name));
            }

            return reliableMessage;
        }

        static string CalculateRowKey(string deviceId, int packetId) => deviceId + "_" + packetId.ToString(CultureInfo.InvariantCulture);

        static string CalculatePartitionKey(string identifier)
        {
            byte[] hash = MurmurHash.Create32(0, false).ComputeHash(Encoding.ASCII.GetBytes(identifier));
            return PartitionKeys[BitConverter.ToUInt32(hash, 0) % PartitionCount];
        }
    }
}