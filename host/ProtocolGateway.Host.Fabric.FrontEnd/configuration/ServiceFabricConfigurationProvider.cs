namespace ProtocolGateway.Host.Fabric.FrontEnd.Configuration
{
    #region Using Clauses
    using System.Reflection;
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using FabricShared.Logging;
    using Microsoft.Azure.Devices.ProtocolGateway;
    #endregion

    /// <summary>
    /// A simple configuration provider for DotNetty based on common Service Fabric techniques to manage configuration
    /// </summary>
    public class ServiceFabricConfigurationProvider : ISettingsProvider
    {
        #region Variables
        /// <summary>
        /// The component name used for debug and diagnostics messages
        /// </summary>
        readonly string componentName;

        /// <summary>
        /// The service fabric logger to be used when writing debug and diagnostics information
        /// </summary>
        readonly IServiceLogger logger;

        /// <summary>
        /// The properties and their values retrieved from the configuration object
        /// </summary>
        readonly ImmutableDictionary<string, string> configurationValues;
        #endregion
        #region Constructors
        /// <summary>
        /// The settings provider used to bridge Service Fabric configuration information and DotNetty
        /// </summary>
        /// <param name="traceId">The unique identifier used to correlate debugging and diagnostics messages</param>
        /// <param name="componentName">The component name used for debug and diagnostics messages</param>
        /// <param name="logger">The service fabric logger to be used when writing debug and diagnostics information</param>
        /// <param name="gatewayConfiguration">The gateway configuration used to get current configuration information for DotNetty</param>
        /// <param name="iotHubConfiguration">The IotHub Client configuration used to get current configuration information for DotNetty</param>
        /// <param name="mqttConfiguration">The MQTT configuration used to get current configuration information for DotNetty</param>
        public ServiceFabricConfigurationProvider(Guid traceId, string componentName, IServiceLogger logger, GatewayConfiguration gatewayConfiguration, IoTHubConfiguration iotHubConfiguration, MqttServiceConfiguration mqttConfiguration)
        {
            this.componentName = componentName;
            this.logger = logger;

            this.logger.Verbose(traceId, this.componentName, "Initializing configuration provider.");

            var baseProperties = new Dictionary<string, string>();

            foreach (PropertyInfo element in typeof(GatewayConfiguration).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!element.PropertyType.IsArray)
                {
                    object value = element.GetValue(gatewayConfiguration, null);
                    baseProperties.Add(element.Name, element.PropertyType == typeof(string) ? value as string : value.ToString());
                }
            }

            foreach (PropertyInfo element in typeof(MqttServiceConfiguration).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!element.PropertyType.IsArray)
                {
                    object value = element.GetValue(mqttConfiguration, null);
                    baseProperties.Add(element.Name, element.PropertyType == typeof(string) ? value as string : value.ToString());
                }
            }

            foreach (PropertyInfo element in typeof(IoTHubConfiguration).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!element.PropertyType.IsArray)
                {
                    object value = element.GetValue(iotHubConfiguration, null);
                    baseProperties.Add($"IotHubClient.{element.Name}", value?.ToString());
                }
            }



            this.configurationValues = baseProperties.ToImmutableDictionary();
            this.logger.Informational(traceId, this.componentName, "Initializing configuration provider complete.");
        }
        #endregion
        #region Methods
        /// <summary>
        /// Retrieves the setting by name from the Service Fabric configuration object
        /// </summary>
        /// <param name="name">The name of the configuration setting to retrieve</param>
        /// <param name="value">The value of the settting</param>
        /// <returns>True if the value was successfully retrieved</returns>
        public bool TryGetSetting(string name, out string value)
        {
            bool success = false;
            Guid traceId = Guid.NewGuid();

            this.logger.Verbose(traceId, this.componentName, "Requested setting {0}.", new object[] { name });

            if (this.configurationValues.TryGetValue(name, out value))
            {
                success = true;
            }
            else
            {
                this.logger.Verbose(traceId, this.componentName, "Setting not found.");
            }

            return success;
        }
        #endregion
    }
}
