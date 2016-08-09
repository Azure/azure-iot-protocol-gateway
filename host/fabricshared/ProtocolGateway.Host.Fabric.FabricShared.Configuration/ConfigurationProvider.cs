namespace ProtocolGateway.Host.Fabric.FabricShared.Configuration
{
    #region Using Clauses
    using System;
    using System.Text;
    using System.Collections.ObjectModel;
    using System.Fabric;
    using System.Fabric.Description;
    using System.IO;
    using System.Security;
    using System.Security.Cryptography;
    using System.ComponentModel;
    using Logging;
    using Properties;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using Security;
    #endregion

    /// <summary>
    /// Generic configuration provider.
    /// </summary>
    public sealed class ConfigurationProvider<TConfiguration> : IConfigurationProvider<TConfiguration> 
        where TConfiguration : class
    {
        #region Constants
        /// <summary>
        /// Name of the configuration package object.
        /// </summary>
        const string ConfigurationPackageObjectName = @"Config";

        /// <summary>
        /// The component name for debug and diagnostics messages
        /// </summary>
        const string ComponentName = @"Configuration Provider";
        #endregion
        #region Events
        /// <summary>
        /// Configuration changed event for property based configuration changes.
        /// </summary>
        public event EventHandler ConfigurationPropertyChangedEvent;

        /// <summary>
        /// Configuration changed event for property based configuration changes.
        /// </summary>
        public event EventHandler<ConfigurationChangedEventArgs<TConfiguration>> ConfigurationChangedEvent;
        #endregion
        #region Variables
        /// <summary>
        /// Uri of the service type instance.
        /// </summary>
        readonly Uri serviceNameUri;

        /// <summary>
        /// Name of the service type instance.
        /// </summary>
        readonly string serviceName;

        /// <summary>
        /// Current configuration settings.
        /// </summary>
        TConfiguration configFile;

        /// <summary>
        /// MD5 hash for the configuration file.
        /// </summary>
        byte[] configFileHash;

        /// <summary>
        /// ConfigurationSection for this service type.
        /// </summary>
        ConfigurationSection configSection;

        /// <summary>
        /// IServiceEventSource instance used for logging.
        /// </summary>
        readonly IServiceLogger logger;

        /// <summary>
        /// MD5 hash for the configuration section.
        /// </summary>
        byte[] configSectionHash;
        #endregion
        #region Properties
        /// <summary>
        /// Gets the configuration settings values.
        /// </summary>
        public TConfiguration Config => this.configFile;

        /// <summary>
        /// The name of the configuration to load. This will be appended to the name of the service when looking for the file (e.g. FrontEnd.Name.json)
        /// </summary>
        public string ConfigurationName { get; }
        #endregion
        #region Constructors
        /// <summary>
        /// ConfigurationProvider constructor.
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlate debugging and diagnostics messages.</param>
        /// <param name="serviceName">Name of the service.</param>
        /// <param name="context">CodePackageActivationContext instance.</param>
        /// <param name="logger">IServiceEventSource instance for logging.</param>
        /// <param name="configurationName">The name of the configuration to load. This will be appended to the name of the service when looking for the file (e.g. FrontEnd.Name.json)</param>
        public ConfigurationProvider(Guid traceId, Uri serviceName, ICodePackageActivationContext context, IServiceLogger logger, string configurationName = null)
        {
            if (serviceName == null) throw new ArgumentNullException(nameof(serviceName));
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (logger == null) throw new ArgumentNullException(nameof(logger));

            logger.Verbose(traceId, ComponentName, "Initializing.");

            this.serviceNameUri = serviceName;
            this.serviceName = this.serviceNameUri.Segments[this.serviceNameUri.Segments.Length - 1];
            this.logger = logger;
            this.ConfigurationName = configurationName;

            // Subscribe to configuration change events if the context was passed. It will not be passed for unit tests.
            context.ConfigurationPackageAddedEvent += this.ConfigurationPackageAdded;
            context.ConfigurationPackageModifiedEvent += this.ConfigurationPackageModified;
            context.ConfigurationPackageRemovedEvent += this.ConfigurationPackageRemoved;

            // Create the add event parameters and call.
            var packageEvent = new PackageAddedEventArgs<ConfigurationPackage>
            {
                Package = context.GetConfigurationPackageObject(ConfigurationPackageObjectName)
            };

            this.ConfigurationPackageAdded(null, packageEvent);

            logger.Verbose(traceId, ComponentName, "Finished initializing.");
        }
        #endregion
        #region Private Methods
        /// <summary>
        /// Retrieves the configuration section and identifies if the section data has changeds
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlate debugging and diagnostics messages</param>
        /// <param name="sections">Collection of keyed ConfigurationSections.</param>
        void LoadConfigurationSection(Guid traceId, KeyedCollection<string, ConfigurationSection> sections)
        {
            if (sections.Contains(this.serviceNameUri.AbsoluteUri))
            {
                this.logger.Verbose(traceId, ComponentName, "Configuration for service found ({0})", new object[] { this.serviceNameUri.AbsoluteUri });

                ConfigurationSection section = sections[this.serviceNameUri.AbsoluteUri];

                // Create an MD5 to hash the values to determine if they have changed.
                using (MD5 md5 = MD5.Create())
                {
                    byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(section.ToString()));

                    if (!this.CompareHashes(hash, this.configSectionHash))
                    {
                        this.logger.Informational(traceId, ComponentName, "MD5 matched");

                        // Set the provider values.
                        this.configSection = section;
                        this.configSectionHash = hash;

                        // If necessary, call the property changed event.
                        this.ConfigurationPropertyChangedEvent?.Invoke(this, EventArgs.Empty);
                    }
                    else
                    {
                        this.logger.Verbose(traceId, ComponentName, "MD5 matched");
                    }
                }
            }
            else
            {
                this.logger.Informational(traceId, ComponentName, "Configuration for service not found ({0})", new object[] { this.serviceNameUri.AbsoluteUri });
            }
        }

        /// <summary>
        /// Loads the configuration.
        /// </summary>
        /// <param name="path">Path to the directory containing configuration information.</param>
        /// <param name="sections">Collection of keyed ConfigurationSections.</param>
        /// <remarks>Loads the configuration with the same name as the service type (e.g. [ServiceTypeName].XML. This
        /// method was constructed this way to enable some level of unit testing. ConfigurationSection cannot be created or mocked.</remarks>
        void LoadConfiguration(string path, KeyedCollection<string, ConfigurationSection> sections)
        {
            Guid traceId = Guid.NewGuid();

            if (path == null) throw new ArgumentNullException(nameof(path));
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException(string.Format(Resources.EmptyOrWhitespace, nameof(path)), nameof(path));
            if (sections == null) throw new ArgumentNullException(nameof(sections));

            this.logger.ServicePartitionConfigurationChanged(traceId, ComponentName, this.serviceName);

            try
            {
                this.LoadConfigurationSection(traceId, sections);
                string fileSuffix = string.IsNullOrWhiteSpace(this.ConfigurationName) ? "" : $".{this.ConfigurationName }";

                // Create an MD5 to hash the values to determine if they have changed.
                using (MD5 md5 = MD5.Create())
                {
                    // Second, attempt to load a file of the format <service name>.json. Use the last segment of the service name Uri for the file name.
                    string filepath = Path.Combine(path, $"{this.serviceName}{ fileSuffix }.json");

                    this.logger.Verbose(traceId, ComponentName, "Attempting to find file at {0}", new object[] { filepath });

                    // Stream the file while calculating the MD5 hash of the contents.
                    using (var fs = new FileStream(filepath, FileMode.Open, FileAccess.Read))
                    using (var cs = new CryptoStream(fs, md5, CryptoStreamMode.Read))
                    using (var reader = new StreamReader(cs))
                    {
                        string json = reader.ReadToEnd();
                        var settings = new JsonSerializerSettings();
                        settings.Converters.Add(new IsoDateTimeConverter());
                        var result = JsonConvert.DeserializeObject<TConfiguration>(json, settings);
                        byte[] fileHash = md5.Hash;

                        if (!this.CompareHashes(fileHash, this.configFileHash))
                        {
                            this.logger.Informational(traceId, ComponentName, "MD5 did not matched");

                            // If necessary, call the class changed event.
                            this.ConfigurationChangedEvent?.Invoke(this, new ConfigurationChangedEventArgs<TConfiguration>(this.serviceName, this.configFile, result, this.ConfigurationName));

                            // Set the provider values.
                            this.configFileHash = fileHash;
                            this.configFile = result;
                        }
                        else
                        {
                            this.logger.Verbose(traceId, ComponentName, "MD5 matched");
                        }
                    }
                }
            }
            catch (FileNotFoundException e)
            {
                this.logger.Warning(traceId, ComponentName, "The configuration file was not found.", null, e);
            }
        }

        /// <summary>
        /// Compares two hash values for equality.
        /// </summary>
        /// <param name="hash1">Byte array containing the hash value.</param>
        /// <param name="hash2">Byte array containing the hash value.</param>
        /// <returns>True if equal, otherwise false.</returns>
        bool CompareHashes(byte[] hash1, byte[] hash2)
        {
            bool isEqual = false;

            if (((hash1 != null) && (hash2 != null)) && (hash1.Length == hash2.Length))
            {
                int index = -1;
                bool differenceFound = false;

                while (++index < hash1.Length && !differenceFound)
                {
                    differenceFound = hash1[index] != hash2[index];
                }

                isEqual = !differenceFound;
            }

            return isEqual;
        }
        #endregion
        #region Event Handlers
        /// <summary>
        /// Called when a new configuration package has been added during a deployment.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="e">PackageAddedEventArgs&lt;ConfigurationPackage&gt; instance.</param>
        void ConfigurationPackageAdded(object sender, PackageAddedEventArgs<ConfigurationPackage> e)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));

            // Attempt to load the configuration.
            this.LoadConfiguration(e.Package.Path, e.Package.Settings.Sections);
        }

        /// <summary>
        /// Called when a change to an existing configuration package has been deployed.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="e">PackageAddedEventArgs&lt;ConfigurationPackage&gt; instance.</param>
        void ConfigurationPackageModified(object sender, PackageModifiedEventArgs<ConfigurationPackage> e)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));

            // Attempt to load the configuration. Only load the new ones, the old settings are cached within the provider.
            this.LoadConfiguration(e.NewPackage.Path, e.NewPackage.Settings.Sections);
        }

        /// <summary>
        /// Called when am existing configuration package has been removed.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="e">PackageAddedEventArgs&lt;ConfigurationPackage&gt; instance.</param>
        void ConfigurationPackageRemoved(object sender, PackageRemovedEventArgs<ConfigurationPackage> e)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));

            // Attempt to load the configuration.
            this.LoadConfiguration(e.Package.Path, e.Package.Settings.Sections);
        }
        #endregion
        #region Public Methods
        /// <summary>
        /// Get a configuration value
        /// </summary>
        /// <param name="key">Name of the configuration item. Key value is case sensitive.</param>
        /// <param name="defaultValue">Default value to return.</param>
        /// <returns>Value of the configuration setting or the default value.</returns>
        public string GetConfigurationString(string key, string defaultValue = "")
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException(string.Format(Resources.EmptyOrWhitespace, nameof(key)), nameof(key));

            if ((this.configSection == null) || (!this.configSection.Parameters.Contains(key)))
                return defaultValue;

            // Get the property and return the value. If the item is encrypted then the encrypted string is returned without warning.
            ConfigurationProperty property = this.configSection.Parameters[key];
            return property.Value;
        }

        /// <summary>
        /// Gets an encrypted configuration value.
        /// </summary>
        /// <param name="key">Name of the configuration item. Key value is case sensitive.</param>
        /// <param name="defaultValue">Default value to return.</param>
        /// <returns>Value of the configuration setting.</returns>
        /// <exception cref="InvalidOperationException">The value of the configuration item is not encrypted.</exception>
        public SecureString GetEncryptedConfigurationString(string key, string defaultValue)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException(string.Format(Resources.EmptyOrWhitespace, nameof(key)), nameof(key));

            SecureString value;

            // Check if the configuration provider is null, if it is return the default value.
            if ((this.configSection == null) || (!this.configSection.Parameters.Contains(key)))
            {
                value = defaultValue?.GetSecureString();
            }
            else if (this.configSection.Parameters[key].IsEncrypted)
            {
                value = this.configSection.Parameters[key].DecryptValue();
            }
            else
            {
                value = this.configSection.Parameters[key].Value?.GetSecureString();
            }

            return value;
        }

        /// <summary>
        /// Get a configuration value
        /// </summary>
        /// <typeparam name="T">A value type</typeparam>
        /// <param name="key">Name of the configuration item. Key value is case sensitive.</param>
        /// <param name="defaultValue">Default value to return.</param>>
        /// <returns>Value of the configuration setting or the default value.</returns>
        public T GetConfigurationValue<T>(string key, T defaultValue)
        {
            T value = defaultValue;
            string stringRepresentation = this.GetConfigurationString(key, defaultValue.ToString());

            if (stringRepresentation != null)
            {
                TypeConverter converter = TypeDescriptor.GetConverter(typeof(T));
                object fromString = converter.ConvertFromString(stringRepresentation);

                if (fromString != null)
                {
                    value = (T)fromString;
                }
            }

            return value;
        }

        /// <summary>
        /// Gets a configuration value of the specified type from a JSON string
        /// </summary>
        /// <param name="key">Name of the configuration item. Key value is case sensitive.</param>
        /// <param name="defaultValue">Default value to return.</param>
        /// <returns>Value of the configuration item or the default value.</returns>
        public T GetConfigurationJsonValue<T>(string key, T defaultValue)
        {
            T value = defaultValue;
            string stringRepresentation = this.GetConfigurationString(key, defaultValue.ToString());

            if (stringRepresentation != null)
            {
                value = JsonConvert.DeserializeObject<T>(stringRepresentation);
            }

            return value;
        }
        #endregion
    }
}
