// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Providers.CloudStorage
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.ProtocolGateway.Identity;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Table;
    using Murmur;

    public class TableQos2StatePersistenceProvider : IQos2StatePersistenceProvider
    {
        const int PartitionCount = 0x10; // WARNING: changing partition count will change placement of rows in partitions. Do it only if table is wiped or migrated along the change.
        const int PartitionCountModulusMask = 0xF;

        static readonly string[] PartitionKeys = Enumerable.Range(0, PartitionCount).Select(i => (i + 1).ToString()).ToArray();

        readonly CloudTable table;

        internal TableQos2StatePersistenceProvider(string connectionString, string tableName)
        {
            CloudStorageAccount cloudStorageAccount;
            if (!CloudStorageAccount.TryParse(connectionString, out cloudStorageAccount))
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture,
                    "Could not parse CloudStorageAccount having value: {0}",
                    connectionString));
            }

            CloudTableClient tableClient = cloudStorageAccount.CreateCloudTableClient();
            tableClient.BufferManager = StorageBufferManager.Shared;
            this.table = tableClient.GetTableReference(tableName);
        }

        public static async Task<TableQos2StatePersistenceProvider> CreateAsync(string connectionString, string containerName)
        {
            var manager = new TableQos2StatePersistenceProvider(connectionString, containerName);
            await manager.InitializeAsync();
            return manager;
        }

        async Task InitializeAsync()
        {
            try
            {
                await this.table.CreateIfNotExistsAsync();
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("Failed to initialize {0}.", this.GetType().Name), ex); // todo: custom exception type
            }
        }

        public IQos2MessageDeliveryState Create(ulong sequenceNumber)
        {
            return new TableMessageDeliveryState(sequenceNumber);
        }

        public async Task<IQos2MessageDeliveryState> GetMessageAsync(IDeviceIdentity deviceIdentity, int packetId)
        {
            string rowKey = CalculateRowKey(deviceIdentity.Id, packetId);
            TableQuery<TableMessageDeliveryState> query = new TableQuery<TableMessageDeliveryState>()
                .Where(TableQuery.CombineFilters(
                    TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, CalculatePartitionKey(rowKey)),
                    TableOperators.And,
                    TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, rowKey)));
            return await this.ReadRowAsync(query, CancellationToken.None);
        }

        public Task DeleteMessageAsync(IDeviceIdentity deviceIdentity, int packetId, IQos2MessageDeliveryState message)
        {
            Contract.Requires(message != null);

            TableMessageDeliveryState tableMessage = ValidateMessageType(message);

            if (tableMessage.ETag == null)
            {
                throw new ArgumentException("Transient message cannot be deleted.");
            }

            return this.table.ExecuteAsync(TableOperation.Delete(tableMessage));
        }

        public Task SetMessageAsync(IDeviceIdentity deviceIdentity, int packetId, IQos2MessageDeliveryState message)
        {
            Contract.Requires(message != null);

            TableMessageDeliveryState tableMessage = ValidateMessageType(message);
            TableOperation tableOperation;
            if (tableMessage.ETag == null)
            {
                string rowKey = CalculateRowKey(deviceIdentity.Id, packetId);
                tableMessage.PartitionKey = CalculatePartitionKey(rowKey);
                tableMessage.RowKey = rowKey;
                tableOperation = TableOperation.Insert(tableMessage);
            }
            else
            {
                tableOperation = TableOperation.Replace(tableMessage);
            }
            return this.table.ExecuteAsync(tableOperation);
        }

        static TableMessageDeliveryState ValidateMessageType(IQos2MessageDeliveryState message)
        {
            var tableMessage = message as TableMessageDeliveryState;
            if (tableMessage == null)
            {
                throw new ArgumentException(string.Format("Message is of unexpected type `{0}`. Only messages created through {1}.Create() method are supported",
                    message.GetType().Name, typeof(TableQos2StatePersistenceProvider).Name));
            }
            return tableMessage;
        }

        async Task<TableMessageDeliveryState> ReadRowAsync(TableQuery<TableMessageDeliveryState> query, CancellationToken cancellationToken)
        {
            TableContinuationToken continuationToken = null;
            do
            {
                TableQuerySegment<TableMessageDeliveryState> queryResult = await this.table.ExecuteQuerySegmentedAsync<TableMessageDeliveryState>(query, continuationToken, null, null, cancellationToken);
                List<TableMessageDeliveryState> results = queryResult.Results;
                if (results.Count > 1)
                {
                    throw new InvalidOperationException("Query returned more than one result.");
                }
                else if (results.Count == 1)
                {
                    return results[0];
                }
                continuationToken = queryResult.ContinuationToken;
            }
            while (continuationToken != null);

            return null;
        }

        static string CalculateRowKey(string deviceId, int packetId) => deviceId + "_" + packetId.ToString(CultureInfo.InvariantCulture);

        static string CalculatePartitionKey(string rowKey)
        {
            byte[] hash = MurmurHash.Create32(0, false).ComputeHash(Encoding.ASCII.GetBytes(rowKey));
            return PartitionKeys[hash[BitConverter.IsLittleEndian ? 0 : 3] & PartitionCountModulusMask];
        }
    }
}