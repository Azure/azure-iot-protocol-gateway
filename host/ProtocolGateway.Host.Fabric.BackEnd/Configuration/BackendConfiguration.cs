// <copyright file="BackendConfiguration.cs" company="Microsoft">
//   BackendConfiguration
// </copyright>
// <summary>
//   Defines the BackendConfiguration type.
// </summary>

namespace ProtocolGateway.Host.Fabric.BackEnd.Configuration
{
    /// <summary>
    /// The backend configuration.
    /// </summary>
    public class BackendConfiguration
    {
        /// <summary>
        /// Gets or sets the backup mode.
        /// </summary>
        public BackupMode BackupMode { get; set; }

        /// <summary>
        /// Gets or sets the local backup configuration.
        /// </summary>
        public LocalBackupConfiguration LocalBackupConfiguration { get; set; }

        /// <summary>
        /// Gets or sets the session dictionary name.
        /// </summary>
        public string SessionDictionaryName { get; set; }

        /// <summary>
        /// Gets or sets the QOS 2 dictionary name.
        /// </summary>
        public string Qos2DictionaryName { get; set; }

        public AzureBackupConfiguration AzureBackupConfiguration { get; set; }
    }

    public enum BackupMode
    {
        Azure,
        Local,
        None
    }
}
