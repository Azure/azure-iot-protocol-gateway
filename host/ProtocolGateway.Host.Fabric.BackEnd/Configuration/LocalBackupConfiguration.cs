namespace ProtocolGateway.Host.Fabric.BackEnd.Configuration
{
    /// <summary>
    /// The local backup configuration.
    /// </summary>
    public class LocalBackupConfiguration
    {
        /// <summary>
        /// Gets or sets the backup archival path.
        /// </summary>
        public string BackupArchivalPath { get; set; }

        /// <summary>
        /// Gets or sets the backup frequency in secondse.
        /// </summary>
        public string BackupFrequencyInSeconds { get; set; }

        /// <summary>
        /// Gets or sets the max backups to keep.
        /// </summary>
        public string MaxBackupsToKeep { get; set; }
    }
}
