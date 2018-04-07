namespace ProtocolGateway.Host.Fabric.BackEnd
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Tracing;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.ProtocolGateway.Instrumentation;
    using Microsoft.ServiceFabric.Data;
    using ProtocolGateway.Host.Fabric.BackEnd.Configuration;
    using ProtocolGateway.Host.Fabric.FabricShared.Logging;

    internal class DiskBackupManager : IBackupManager
    {
        const string ComponentName = @"DiskBackupManager";

        readonly string partitionArchiveFolder;
        readonly string partitionTempDirectory;
        readonly long backupFrequencyInSeconds;
        readonly int maxBackupsToKeep;
        readonly string partitionId;
        readonly IServiceLogger logger;
        readonly Guid traceId = Guid.NewGuid();

        public DiskBackupManager(LocalBackupConfiguration localConfigSection, string partitionId, string codePackageTempDirectory, IServiceLogger logger)
        {
            string backupArchivalPath = localConfigSection.BackupArchivalPath;
            this.backupFrequencyInSeconds = long.Parse(localConfigSection.BackupFrequencyInSeconds);
            this.maxBackupsToKeep = int.Parse(localConfigSection.MaxBackupsToKeep);
            this.partitionId = partitionId;
            this.logger = logger;

            this.partitionArchiveFolder = Path.Combine(backupArchivalPath, "Backups", partitionId);
            this.partitionTempDirectory = Path.Combine(codePackageTempDirectory, partitionId);

            this.logger.Verbose(this.traceId, ComponentName, $"DiskBackupManager constructed Interval in Sec:{this.backupFrequencyInSeconds}, archivePath:{this.partitionArchiveFolder}, tempPath:{this.partitionTempDirectory}, backupsToKeep:{this.maxBackupsToKeep}");
        }

        long IBackupManager.backupFrequencyInSeconds => this.backupFrequencyInSeconds;

        public Task ArchiveBackupAsync(BackupInfo backupInfo, CancellationToken cancellationToken)
        {
            string fullArchiveDirectory = Path.Combine(this.partitionArchiveFolder, $"{ DateTime.UtcNow:u}");

            var dirInfo = new DirectoryInfo(fullArchiveDirectory);
            dirInfo.Create();

            string fullArchivePath = Path.Combine(fullArchiveDirectory, "Backup.zip");

            ZipFile.CreateFromDirectory(backupInfo.Directory, fullArchivePath, CompressionLevel.Fastest, false);

            var backupDirectory = new DirectoryInfo(backupInfo.Directory);
            backupDirectory.Delete(true);

            return Task.FromResult(true);
        }

        public Task<string> RestoreLatestBackupToTempLocation(CancellationToken cancellationToken)
        {
            this.logger.Verbose(this.traceId, ComponentName, $"Restoring backup to temp source:{this.partitionArchiveFolder} destination:{this.partitionTempDirectory}");
            
            var dirInfo = new DirectoryInfo(this.partitionArchiveFolder);

            string backupZip = dirInfo.GetDirectories().OrderByDescending(x => x.LastWriteTime).First().FullName;

            string zipPath = Path.Combine(backupZip, "Backup.zip");

            this.logger.Verbose(this.traceId, ComponentName, $"latest zip backup is {zipPath}");

            var directoryInfo = new DirectoryInfo(this.partitionTempDirectory);
            if (directoryInfo.Exists)
            {
                directoryInfo.Delete(true);
            }

            directoryInfo.Create();

            ZipFile.ExtractToDirectory(zipPath, this.partitionTempDirectory);

            this.logger.Verbose(this.traceId, ComponentName, $"Zip backup {zipPath} extracted to {this.partitionTempDirectory}");

            return Task.FromResult(this.partitionTempDirectory);
        }

        public Task DeleteBackupsAsync(CancellationToken cancellationToken)
        {
            return Task.Run(
                () =>
                {
                    this.logger.Verbose(this.traceId, ComponentName, "deleting old backups");

                    if (!Directory.Exists(this.partitionArchiveFolder))
                    {
                        //Nothing to delete; Backups may not even have been created for the partition
                        return;
                    }

                    var dirInfo = new DirectoryInfo(this.partitionArchiveFolder);

                    IEnumerable<DirectoryInfo> oldBackups = dirInfo.GetDirectories().OrderByDescending(x => x.LastWriteTime).Skip(this.maxBackupsToKeep);

                    foreach (DirectoryInfo oldBackup in oldBackups)
                    {
                        this.logger.Verbose(this.traceId, ComponentName, $"Deleting old backup {oldBackup.FullName}");

                        oldBackup.Delete(true);
                    }

                    this.logger.Verbose(this.traceId, ComponentName, "Old backups deleted");
                },
                cancellationToken);
        }
    }
}