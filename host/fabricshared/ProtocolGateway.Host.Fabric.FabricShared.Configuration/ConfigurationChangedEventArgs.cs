namespace ProtocolGateway.Host.Fabric.FabricShared.Configuration
{
    #region Using Clauses
    using System;
    #endregion

    /// <summary>
    /// Configuration class changed event arguments.
    /// </summary>
    /// <typeparam name="TConfiguration"></typeparam>
    public sealed class ConfigurationChangedEventArgs<TConfiguration> : EventArgs where TConfiguration : class
    {
        #region Properties
        /// <summary>
        /// Name of the changed configuration.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Configuration class instance.
        /// </summary>
        public TConfiguration CurrentConfiguration { get; }

        /// <summary>
        /// Configuration class instance.
        /// </summary>
        public TConfiguration NewConfiguration { get; }

        /// <summary>
        /// The name of the configuration
        /// </summary>
        public string ConfigurationName { get; }
        #endregion
        #region Constructors
        /// <summary>
        /// ConfigurationClassChangeEventArgs constructor.
        /// </summary>
        /// <param name="name">Name of the changed configuration.</param>
        /// <param name="currentConfig">Current configuration instance of type TConfiguration.</param>
        /// <param name="newConfig">New configuration instance of type TConfiguration.</param>
        /// <param name="configurationName">The name of the configuration.</param>
        public ConfigurationChangedEventArgs(string name, TConfiguration currentConfig, TConfiguration newConfig, string configurationName)
        {
            this.Name = name;
            this.CurrentConfiguration = currentConfig;
            this.NewConfiguration = newConfig;
            this.ConfigurationName = configurationName;
        }
        #endregion
    }
}
