namespace ProtocolGateway.Host.Fabric.FabricShared.Configuration
{
    #region Using Clauses
    using System.Security;
    #endregion

    /// <summary>
    /// ConfigurationProvider interface.
    /// </summary>
    public interface IConfigurationProvider<out TConfiguration> where TConfiguration : class
    {
        #region Properties
        /// <summary>
        /// Gets the configuration settings values.
        /// </summary>
        TConfiguration Config { get; }

        /// <summary>
        /// The name of the configuration to load. This will be appended to the name of the service when looking for the file (e.g. FrontEnd.Name.json)
        /// </summary>
        string ConfigurationName { get; }
        #endregion
        #region Methods
        /// <summary>
        /// Get a configuration value
        /// </summary>
        /// <param name="key">Name of the configuration item. Key value is case sensitive.</param>
        /// <param name="defaultValue">Default value to return.</param>
        /// <returns>Value of the configuration setting or the default value.</returns>
        string GetConfigurationString(string key, string defaultValue);

        /// <summary>
        /// Gets an encrypted configuration value.
        /// </summary>
        /// <param name="key">Name of the configuration item. Key value is case sensitive.</param>
        /// <param name="defaultValue">Default value to return.</param>
        /// <returns>Value of the configuration setting.</returns>
        SecureString GetEncryptedConfigurationString(string key, string defaultValue);

        /// <summary>
        /// Gets a configuration value of the specified type
        /// </summary>
        /// <param name="key">Name of the configuration item. Key value is case sensitive.</param>
        /// <param name="defaultValue">Default value to return.</param>
        /// <returns>Value of the configuration item or the default value.</returns>
        T GetConfigurationValue<T>(string key, T defaultValue);

        /// <summary>
        /// Gets a configuration value of the specified type from a JSON string
        /// </summary>
        /// <param name="key">Name of the configuration item. Key value is case sensitive.</param>
        /// <param name="defaultValue">Default value to return.</param>
        /// <returns>Value of the configuration item or the default value.</returns>
        T GetConfigurationJsonValue<T>(string key, T defaultValue);
        #endregion
    }
}
