namespace ProtocolGateway.Host.Fabric.BackEnd.Configuration
{
    public class AzureBackupConfiguration
    {
        /// <summary>
        /// Gets or sets the backup frequency in secondse.
        /// </summary>
        public string BackupFrequencyInSeconds { get; set; }

        /// <summary>
        /// Gets or sets the max backups to keep.
        /// </summary>
        public string MaxBackupsToKeep { get; set; }

        public string BlobConnectionString { get; set; }
    }
}