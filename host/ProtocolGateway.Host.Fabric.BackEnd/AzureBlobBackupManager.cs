namespace ProtocolGateway.Host.Fabric.BackEnd
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Tracing;
    using System.Globalization;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.ProtocolGateway.Instrumentation;
    using Microsoft.ServiceFabric.Data;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Blob;

    using ProtocolGateway.Host.Fabric.BackEnd.Configuration;
    using ProtocolGateway.Host.Fabric.FabricShared.Logging;

    internal class AzureBlobBackupManager : IBackupManager
    {
        const string ComponentName = @"AzureBackupManager";

        readonly string partitionId;
        readonly IServiceLogger logger;
        readonly long backupFrequency;
        readonly int maxBackupsToKeep;
        readonly string tempDirectory;
        readonly CloudBlobContainer backupBlobContainer;
        readonly Guid traceId = Guid.NewGuid(); 

        public AzureBlobBackupManager(AzureBackupConfiguration azureBackupConfigSection, string partitionId, string tempDirectory, IServiceLogger logger)
        {
            this.backupFrequency = long.Parse(azureBackupConfigSection.BackupFrequencyInSeconds);
            this.maxBackupsToKeep = int.Parse(azureBackupConfigSection.MaxBackupsToKeep);
            this.partitionId = partitionId;
            this.logger = logger;
            this.tempDirectory = Path.Combine(tempDirectory, partitionId);

            CloudStorageAccount cloudStorageAccount;
            if (!CloudStorageAccount.TryParse(azureBackupConfigSection.BlobConnectionString, out cloudStorageAccount))
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Could not parse CloudStorageAccount having value: {0}", azureBackupConfigSection.BlobConnectionString));
            }

            var cloudblobClient = cloudStorageAccount.CreateCloudBlobClient();
            this.backupBlobContainer = cloudblobClient.GetContainerReference(this.partitionId);
            this.backupBlobContainer.CreateIfNotExists();

            this.logger.Verbose(this.traceId, ComponentName, $"AzureBlobBackupManager constructed Interval in Sec:{this.backupFrequency}, archivePath:{cloudblobClient.StorageUri}, tempPath:{this.tempDirectory}, backupsToKeep:{this.maxBackupsToKeep}");
        }

        /// <summary>
        /// The backup frequency in seconds.
        /// </summary>
        long IBackupManager.backupFrequencyInSeconds => this.backupFrequency;

        public async Task ArchiveBackupAsync(BackupInfo backupInfo, CancellationToken cancellationToken)
        {
            this.logger.Verbose(this.traceId, ComponentName, "AzureBlobBackupManager: Archive Called.");

            string fullArchiveDirectory = Path.Combine(this.tempDirectory, Guid.NewGuid().ToString("N"));

            var fullArchiveDirectoryInfo = new DirectoryInfo(fullArchiveDirectory);
            fullArchiveDirectoryInfo.Create();

            string blobName = $"{this.partitionId}_{DateTime.UtcNow:u}_Backup.zip";
            string fullArchivePath = Path.Combine(fullArchiveDirectory, "Backup.zip");

            ZipFile.CreateFromDirectory(backupInfo.Directory, fullArchivePath, CompressionLevel.Fastest, false);

            var backupDirectory = new DirectoryInfo(backupInfo.Directory);
            backupDirectory.Delete(true);

            var blob = this.backupBlobContainer.GetBlockBlobReference(blobName);
            await blob.UploadFromFileAsync(fullArchivePath, CancellationToken.None);

            var dir = new DirectoryInfo(fullArchiveDirectory);
            dir.Delete(true);

            this.logger.Verbose(this.traceId, ComponentName, "AzureBlobBackupManager: UploadBackupFolderAsync: success.");
        }

        public async Task<string> RestoreLatestBackupToTempLocation(CancellationToken cancellationToken)
        {
            this.logger.Verbose(this.traceId, ComponentName, "AzureBlobBackupManager: Download backup async called.");

            var lastBackupBlob = (await this.GetBackupBlobs(true)).First();

            this.logger.Verbose(this.traceId, ComponentName, $"AzureBlobBackupManager: Downloading {lastBackupBlob.Name}");

            string downloadId = Guid.NewGuid().ToString("N");

            string zipPath = Path.Combine(this.tempDirectory, $"{downloadId}_Backup.zip");

            if (!Directory.Exists(this.tempDirectory))
            {
                Directory.CreateDirectory(this.tempDirectory);
            }

            await lastBackupBlob.DownloadToFileAsync(zipPath, FileMode.CreateNew, cancellationToken);

            string restorePath = Path.Combine(this.tempDirectory, downloadId);

            ZipFile.ExtractToDirectory(zipPath, restorePath);

            var zipInfo = new FileInfo(zipPath);
            zipInfo.Delete();

            this.logger.Verbose(this.traceId, ComponentName, $"AzureBlobBackupManager: Downloaded {lastBackupBlob.Name} in to {restorePath}");

            return restorePath;
        }

        public async Task DeleteBackupsAsync(CancellationToken cancellationToken)
        {
            if (await this.backupBlobContainer.ExistsAsync(cancellationToken))
            {
                this.logger.Verbose(this.traceId, ComponentName, "AzureBlobBackupManager: Deleting old backups");

                IEnumerable<CloudBlockBlob> oldBackups = (await this.GetBackupBlobs(true)).Skip(this.maxBackupsToKeep);

                foreach (CloudBlockBlob backup in oldBackups)
                {
                    this.logger.Verbose(this.traceId, ComponentName, $"AzureBlobBackupManager: Deleting {backup.Name}");
                    await backup.DeleteAsync(cancellationToken);
                }
            }
        }

        private async Task<IEnumerable<CloudBlockBlob>> GetBackupBlobs(bool sorted)
        {
            IEnumerable<IListBlobItem> blobs = this.backupBlobContainer.ListBlobs();
            this.logger.Verbose(this.traceId, ComponentName, $"AzureBlobBackupManager: Got {blobs.Count()} blobs");
            var itemizedBlobs = new List<CloudBlockBlob>();

            foreach (var listBlobItem in blobs)
            {
                var cbb = (CloudBlockBlob)listBlobItem;
                await cbb.FetchAttributesAsync();
                itemizedBlobs.Add(cbb);
            }

            if (sorted)
            {
                return itemizedBlobs.OrderByDescending(x => x.Properties.LastModified);
            }
            else
            {
                return itemizedBlobs;
            }
        }
    }
}