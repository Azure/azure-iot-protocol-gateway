namespace ProtocolGateway.Host.Fabric.FrontEnd.Configuration
{
    /// <summary>
    /// Configuration details for the Azure storage provider
    /// </summary>
    public class AzureSessionStateConfiguration
    {
        /// <summary>
        /// Azure Storage connection string to be used by the BlobSessionStatePersistenceProvider to store MQTT session state data. Default value for 
        /// Console sample and Cloud sample's Local configuration:  UseDevelopmentStorage=true . For Cloud deployment the default value is a connection 
        /// string to the specified storage account.
        /// </summary>
        public string BlobConnectionString { get; set; }

        /// <summary>
        /// Azure Storage Blob Container Name to be used by the BlobSessionStatePersistenceProvider to store MQTT session state data. Default value: mqtt-sessions
        /// </summary>
        public string ContainerName { get; set; }

        /// <summary>
        /// Azure Storage connection string to be used by TableQos2StatePersistenceProvider to store QoS 2 delivery information. Default value for Console sample and 
        /// Cloud sample's Local configuration:  UseDevelopmentStorage=true . For Cloud deployment the default value is a connection string to specified storage account.
        /// </summary>
        public string TableConnectionString { get; set; }

        /// <summary>
        /// Azure Storage Table name to be used by the TableQos2StatePersistenceProvider to store QoS 2 delivery information. Default value: mqttqos2
        /// </summary>
        public string TableName { get; set; }
    }
}
