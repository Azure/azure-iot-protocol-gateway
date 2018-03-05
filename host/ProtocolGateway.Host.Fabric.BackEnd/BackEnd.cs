namespace ProtocolGateway.Host.Fabric.BackEnd
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Tracing;
    using System.Fabric;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.ProtocolGateway.Instrumentation;
    using Microsoft.Azure.Devices.ProtocolGateway.Providers.ReliableStorage;
    using Microsoft.ServiceFabric.Data;
    using Microsoft.ServiceFabric.Data.Collections;
    using Microsoft.ServiceFabric.Services.Communication.Runtime;
    using Microsoft.ServiceFabric.Services.Remoting.Runtime;
    using Microsoft.ServiceFabric.Services.Runtime;

    using ProtocolGateway.Host.Fabric.BackEnd.Configuration;
    using ProtocolGateway.Host.Fabric.FabricShared.Configuration;
    using ProtocolGateway.Host.Fabric.FabricShared.Logging;

    /// <summary>
    /// An instance of this class is created for each service replica by the Service Fabric runtime.
    /// </summary>
    internal sealed class BackEnd : StatefulService, IBackEndService
    {
        const string ComponentName = @"Stateful Back End";

        const string BackupCountDictionaryName = "BackupCountingDictionary";

        readonly IServiceLogger logger;

        readonly IConfigurationProvider<BackendConfiguration> backEndConfigurationProvider;

        IBackupManager backupManager;

        //Set local or cloud backup, or none. Disabled is the default. Overridden by config.
        BackupMode backupStorageType;

        public BackEnd(Guid traceId, string systemName, StatefulServiceContext context)
            : base(context)
        {
            this.logger = new ServiceLogger(systemName, context);
            this.backEndConfigurationProvider = new ConfigurationProvider<BackendConfiguration>(traceId, context.ServiceName, context.CodePackageActivationContext, this.logger);
        }

        /// <summary>
        /// Optional override to create listeners (e.g., HTTP, Service Remoting, WCF, etc.) for this service replica to handle client or user requests.
        /// </summary>
        /// <remarks>
        /// For more information on service communication, see https://aka.ms/servicefabricservicecommunication
        /// </remarks>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {
            Guid traceId = Guid.NewGuid();
            this.logger.Verbose(traceId, ComponentName, "CreateServiceReplicaListeners() Invoked.");
            return this.CreateServiceRemotingReplicaListeners();
        }

        /// <summary>
        /// This is the main entry point for your service replica.
        /// This method executes when this replica of your service becomes primary and has write status.
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service replica.</param>
        protected override Task RunAsync(CancellationToken cancellationToken)
        {
            return this.PeriodicTakeBackupAsync(cancellationToken);
        }

        public async Task SetSessionStateAsync(string identityId, ReliableSessionState sessionState)
        {
            using (var tx = this.StateManager.CreateTransaction())
            {
                var sessionStateDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, ReliableSessionState>>(tx, this.backEndConfigurationProvider.Config.SessionDictionaryName).ConfigureAwait(false);
                await sessionStateDictionary.AddOrUpdateAsync(tx, identityId, sessionState, (s, state) => sessionState).ConfigureAwait(false);
                await tx.CommitAsync().ConfigureAwait(false);
            }
        }

        public async Task DeleteSessionStateAsync(string identityId)
        {
            using (var tx = this.StateManager.CreateTransaction())
            {
                var sessionStateDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, ReliableSessionState>>(tx, this.backEndConfigurationProvider.Config.SessionDictionaryName).ConfigureAwait(false);
                await sessionStateDictionary.TryRemoveAsync(tx, identityId).ConfigureAwait(false);
                await tx.CommitAsync().ConfigureAwait(false);
            }
        }

        public async Task<ReliableSessionState> GetSessionStateAsync(string identityId)
        {
            ReliableSessionState sessionState;
            using (var tx = this.StateManager.CreateTransaction())
            {
                var sessionStateDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, ReliableSessionState>>(tx, this.backEndConfigurationProvider.Config.SessionDictionaryName).ConfigureAwait(false);
                var result = await sessionStateDictionary.TryGetValueAsync(tx, identityId).ConfigureAwait(false);
                sessionState = result.HasValue ? result.Value : null;
                await tx.CommitAsync().ConfigureAwait(false);
            }

            return sessionState;
        }

        public async Task<ReliableMessageDeliveryState> GetMessageAsync(string identityId, int packetId)
        {
            var key = $"{identityId}_{packetId}";
            ReliableMessageDeliveryState deliveryState;
            using (var tx = this.StateManager.CreateTransaction())
            {
                var deliveryStateDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, ReliableMessageDeliveryState>>(tx, this.backEndConfigurationProvider.Config.Qos2DictionaryName).ConfigureAwait(false);
                var result = await deliveryStateDictionary.TryGetValueAsync(tx, key).ConfigureAwait(false);
                deliveryState = result.HasValue ? result.Value : null;
                await tx.CommitAsync().ConfigureAwait(false);
            }

            return deliveryState;
        }

        public async Task DeleteMessageAsync(string identityId, int packetId)
        {
            var key = $"{identityId}_{packetId}";
            using (var tx = this.StateManager.CreateTransaction())
            {
                var deliveryStateDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, ReliableMessageDeliveryState>>(tx, this.backEndConfigurationProvider.Config.Qos2DictionaryName).ConfigureAwait(false);
                await deliveryStateDictionary.TryRemoveAsync(tx, key).ConfigureAwait(false);
                await tx.CommitAsync().ConfigureAwait(false);
            }
        }

        public async Task SetMessageAsync(string identityId, int packetId, ReliableMessageDeliveryState message)
        {
            var key = $"{identityId}_{packetId}";
            using (var tx = this.StateManager.CreateTransaction())
            {
                var deliveryStateDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, ReliableMessageDeliveryState>>(tx, this.backEndConfigurationProvider.Config.Qos2DictionaryName).ConfigureAwait(false);
                await deliveryStateDictionary.AddOrUpdateAsync(tx, key, message, (s, state) => message).ConfigureAwait(false);
                await tx.CommitAsync().ConfigureAwait(false);
            }
        }

        async Task PeriodicTakeBackupAsync(CancellationToken cancellationToken)
        {
            long backupsTaken = 0;
            this.SetupBackupManager();

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (this.backupStorageType == BackupMode.None)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(10));
                    }
                    else
                    {
                        await Task.Delay(TimeSpan.FromSeconds(this.backupManager.backupFrequencyInSeconds), cancellationToken).ConfigureAwait(false);

                        var backupDescription = new BackupDescription(BackupOption.Full, this.BackupCallbackAsync);

                        await this.BackupAsync(backupDescription, TimeSpan.FromHours(1), cancellationToken);

                        backupsTaken++;
                    }
                }
                catch (OperationCanceledException)
                {
                }
            }
        }

        async Task<bool> BackupCallbackAsync(BackupInfo backupInfo, CancellationToken cancellationToken)
        {
            Guid traceId = Guid.NewGuid();
            this.logger.Verbose(traceId, ComponentName, $"Backup callback for replica {this.Context.PartitionId}|{ this.Context.ReplicaId}");
            long totalBackupCount;

            IReliableDictionary<string, long> backupCountDictionary =
                await this.StateManager.GetOrAddAsync<IReliableDictionary<string, long>>(BackupCountDictionaryName);
            using (ITransaction tx = this.StateManager.CreateTransaction())
            {
                ConditionalValue<long> value = await backupCountDictionary.TryGetValueAsync(tx, "backupCount");

                if (!value.HasValue)
                {
                    totalBackupCount = 0;
                }
                else
                {
                    totalBackupCount = value.Value;
                }

                await backupCountDictionary.SetAsync(tx, "backupCount", ++totalBackupCount);

                await tx.CommitAsync();
            }

            this.logger.Verbose(traceId, ComponentName, $"Backup count dictionary updated, total backup count is {totalBackupCount}");
            
            try
            {
                this.logger.Verbose(traceId, ComponentName, $"Archiving backup");
                await this.backupManager.ArchiveBackupAsync(backupInfo, cancellationToken);
                this.logger.Verbose(traceId, ComponentName, $"Backup archived");
            }
            catch (Exception e)
            {
                this.logger.Verbose(traceId, ComponentName, $"Archive of backup failed: Source: {backupInfo.Directory} Exception: {e.Message}", error: e);
            }

            await this.backupManager.DeleteBackupsAsync(cancellationToken);

            this.logger.Verbose(traceId, ComponentName, "Backups deleted");
            return true;
        }

        void SetupBackupManager()
        {
            Guid traceId = Guid.NewGuid();
            string partitionId = null;
            if (this.Partition.PartitionInfo.Kind == ServicePartitionKind.Named)
            {
                partitionId = ((NamedPartitionInformation)this.Partition.PartitionInfo).Name;
            }
            else
            {
                throw new ArgumentException("Invalid partition configuration of BackEnd service");
            }

            if (this.Context.CodePackageActivationContext != null)
            {
                var backupSettingValue = this.backEndConfigurationProvider.Config.BackupMode;
                switch (backupSettingValue)
                {
                    case BackupMode.Azure:
                        this.backupStorageType = BackupMode.Azure;
                        var azureBackupConfigSection = this.backEndConfigurationProvider.Config.AzureBackupConfiguration;
                        this.backupManager = new AzureBlobBackupManager(azureBackupConfigSection, partitionId, this.Context.CodePackageActivationContext.TempDirectory, this.logger);
                        break;
                    case BackupMode.Local:
                        this.backupStorageType = BackupMode.Local;
                        var localBackupConfigSection = this.backEndConfigurationProvider.Config.LocalBackupConfiguration;
                        this.backupManager = new DiskBackupManager(localBackupConfigSection, partitionId, this.Context.CodePackageActivationContext.TempDirectory, this.logger);
                        break;
                    case BackupMode.None:
                        this.backupStorageType = BackupMode.None;
                        break;
                    default:
                        throw new ArgumentException("Unknown backup type");
                }

                this.logger.Verbose(traceId, ComponentName, $"Backup Manager Set Up");
            }
        }
    }
}
