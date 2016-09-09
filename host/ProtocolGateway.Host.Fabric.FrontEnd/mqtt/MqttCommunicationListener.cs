namespace ProtocolGateway.Host.Fabric.FrontEnd.Mqtt
{
    #region Using Clauses
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Threading;
    using Microsoft.ServiceFabric.Services.Communication.Runtime;
    using System.Fabric;
    using System.Fabric.Description;
    using System.IO;
    using System.Security.Cryptography.X509Certificates;
    using FabricShared.Logging;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence;
    using Microsoft.Azure.Devices.ProtocolGateway.Providers.CloudStorage;
    using ProtocolGateway.Host.Common;
    using ProtocolGateway.Host.Fabric.FabricShared.Configuration;
    using ProtocolGateway.Host.Fabric.FabricShared.Security;
    using ProtocolGateway.Host.Fabric.FrontEnd.Configuration;

    #endregion

    /// <summary>
    /// A MQTT listener that is hosted by the front end service
    /// </summary>
    sealed class MqttCommunicationListener : ICommunicationListener
    {
        #region Delegates
        /// <summary>
        /// A delegate that represents an unsupported configuration change that occurred
        /// </summary>
        internal delegate void UnsupportedConfigurationChange();
        #endregion
        #region Variables
        /// <summary>
        /// A method to call when an unsupported configuration change occurred.
        /// </summary>
        readonly UnsupportedConfigurationChange unsupportedConfigurationChangeCallback;

        /// <summary>
        /// The logger used to write debugging and diagnostics information
        /// </summary>
        readonly IServiceLogger logger;

        /// <summary>
        /// The Service Fabric service context that the listener is associated with
        /// </summary>
        readonly StatelessServiceContext serviceContext;

        /// <summary>
        /// The name of the component to use for debugging and diagnostics
        /// </summary>
        readonly string componentName;

        /// <summary>
        /// The configuration provider that holds data associated with the gateway settings
        /// </summary>
        readonly IConfigurationProvider<GatewayConfiguration> configurationProvider;

        /// <summary>
        /// A cancellation token source that is cancelled to stop the Mqtt DotNetty listener
        /// </summary>
        CancellationTokenSource serverControl;

        /// <summary>
        /// The DotNetty bootstrapper for the MQTT gateway
        /// </summary>
        Bootstrapper bootStrapper;

        /// <summary>
        /// The bootstrapper runasync task
        /// </summary>
        Task runTask = Task.FromResult<object>(null);
        #endregion
        #region Constructors
        /// <summary>
        /// Initializes a new instance of the type
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlate debugging and diagnostics messages</param>
        /// <param name="componentName">The name of the component to use when writing debugging and diagnostics messages</param>
        /// <param name="serviceContext">The stateless service context that the context of the service running the listener</param>
        /// <param name="logger">A service logger used to write debugging and diagnostics messages</param>
        /// <param name="configurationProvider">The configuration provider for the gateway settings</param>
        /// <param name="unsupportedConfigurationChangeCallback">A method to call when an unsupported configuration change occurs.</param>
        public MqttCommunicationListener(Guid traceId, string componentName, StatelessServiceContext serviceContext, IServiceLogger logger, IConfigurationProvider<GatewayConfiguration> configurationProvider, UnsupportedConfigurationChange unsupportedConfigurationChangeCallback)
        {
            if (serviceContext == null) throw new ArgumentNullException(nameof(serviceContext));
            if (configurationProvider == null) throw new ArgumentNullException(nameof(configurationProvider));
            if (logger == null) throw new ArgumentNullException(nameof(logger));

            this.logger = logger;
            this.componentName = componentName;
            this.serviceContext = serviceContext;
            this.configurationProvider = configurationProvider;
            this.unsupportedConfigurationChangeCallback = unsupportedConfigurationChangeCallback;

            // optimizing IOCP performance
            int minWorkerThreads;
            int minCompletionPortThreads;

            ThreadPool.GetMinThreads(out minWorkerThreads, out minCompletionPortThreads);
            ThreadPool.SetMinThreads(minWorkerThreads, Math.Max(16, minCompletionPortThreads));
            
            this.logger.CreateCommunicationListener(traceId, this.componentName, this.GetType().FullName, this.BuildListeningAddress(this.configurationProvider.Config));
        }
        #endregion
        #region Listener Life Cycle
        /// <summary>
        /// Called when the service fabric needs to specifically abort the listener
        /// </summary>
        public void Abort()
        {
            Guid traceId = Guid.NewGuid();

            this.logger.Informational(traceId, this.componentName, "Aborting Mqtt server.");

            try
            {
                this.logger.Informational(traceId, this.componentName, "Disposing of Mqtt listener.");
                this.serverControl.Cancel(false);
                this.bootStrapper.CloseCompletion.Wait(TimeSpan.FromSeconds(20));
                this.runTask.Wait(3000, CancellationToken.None);
            }
            catch (ObjectDisposedException e)
            {
                this.logger.Error(traceId, this.componentName, "Object disposed error for the Mqtt listener.", null, e);
            }
        }

        /// <summary>
        /// Called by the service fabric when the listener is to close
        /// </summary>
        /// <param name="cancellationToken">A token to monitor for abort requests</param>
        /// <returns>A task that completes when the service is closed</returns>
        public async Task CloseAsync(CancellationToken cancellationToken)
        {
            Guid traceId = Guid.NewGuid();

            this.logger.Informational(traceId, this.componentName, "Closing MQTT Server.");

            try
            {
                this.logger.Informational(traceId, this.componentName, "Disposing of Mqtt listener.");
                this.serverControl.Cancel(false);
                await this.bootStrapper.CloseCompletion.ConfigureAwait(false);
                await this.runTask.ConfigureAwait(false);
            }
            catch (ObjectDisposedException e)
            {
                this.logger.Error(traceId, this.componentName, "Object disposed error for the Mqtt listener.", null, e);
            }
        }

        /// <summary>
        /// Called by the service fabric when the listener is to be started
        /// </summary>
        /// <param name="cancellationToken">A token to monitor for abort requests</param>
        /// <returns>A task that completes when the service is openned</returns>
        public async Task<string> OpenAsync(CancellationToken cancellationToken)
        {
            string publishAddress = null;
            Guid traceId = Guid.NewGuid();
            int threadCount = Environment.ProcessorCount;
            this.serverControl = new CancellationTokenSource();
            GatewayConfiguration configuration = this.configurationProvider.Config;

            this.logger.Informational(traceId, this.componentName, "OpenAsync() invoked.");

            var iotHubConfigurationProvider = new ConfigurationProvider<IoTHubConfiguration>(traceId, this.serviceContext.ServiceName, this.serviceContext.CodePackageActivationContext, this.logger, "IoTHubClient");
            iotHubConfigurationProvider.ConfigurationChangedEvent += (sender, args) => this.unsupportedConfigurationChangeCallback?.Invoke();
            iotHubConfigurationProvider.ConfigurationPropertyChangedEvent += (sender, args) => this.unsupportedConfigurationChangeCallback?.Invoke();
            IoTHubConfiguration iotHubConfiguration = iotHubConfigurationProvider.Config;

            X509Certificate2 certificate = this.GetServerCertificate(configuration);
            this.logger.Informational(traceId, this.componentName, "Certificate retrieved.");

            var mqttConfigurationProvider = new ConfigurationProvider<MqttServiceConfiguration>(traceId, this.serviceContext.ServiceName, this.serviceContext.CodePackageActivationContext, this.logger, "Mqtt");
            mqttConfigurationProvider.ConfigurationChangedEvent += (sender, args) => this.unsupportedConfigurationChangeCallback?.Invoke();
            mqttConfigurationProvider.ConfigurationPropertyChangedEvent += (sender, args) => this.unsupportedConfigurationChangeCallback?.Invoke();
            MqttServiceConfiguration mqttConfiguration = mqttConfigurationProvider.Config;
            
            if (mqttConfiguration != null)
            {
                var mqttInboundTemplates = new List<string>(mqttConfiguration.MqttInboundTemplates);
                var mqttOutboundTemplates = new List<string>(mqttConfiguration.MqttOutboundTemplates);

                ISessionStatePersistenceProvider qosSessionProvider = await this.GetSessionStateProviderAsync(traceId, mqttConfiguration).ConfigureAwait(false);
                IQos2StatePersistenceProvider qos2SessionProvider = await this.GetQos2StateProvider(traceId, mqttConfiguration).ConfigureAwait(false);

                this.logger.Informational(traceId, this.componentName, "QOS Providers instantiated.");

                var settingsProvider = new ServiceFabricConfigurationProvider(traceId, this.componentName, this.logger, configuration, iotHubConfiguration, mqttConfiguration);
                this.bootStrapper = new Bootstrapper(settingsProvider, qosSessionProvider, qos2SessionProvider, mqttInboundTemplates, mqttOutboundTemplates);
                this.runTask = this.bootStrapper.RunAsync(certificate, threadCount, this.serverControl.Token);

                publishAddress = this.BuildPublishAddress(configuration);

                this.logger.Informational(traceId, this.componentName, "Bootstrapper instantiated.");
            }
            else
            {
                this.logger.Critical(traceId, this.componentName, "Failed to start endpoint because Mqtt service configuration is missing.");
            }

            return publishAddress;
        }
        #endregion
        #region QoS Provider
        /// <summary>
        /// Retrieves the session state provider to be used for MQTT Sessions
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlate debugging and diagnostics messages</param>
        /// <param name="configuration">The gateway configuration</param>
        /// <returns>A session state persistence provider if available</returns>
        async Task<ISessionStatePersistenceProvider> GetSessionStateProviderAsync(Guid traceId, MqttServiceConfiguration configuration)
        {
            ISessionStatePersistenceProvider stateProvider = null;

            this.logger.Informational(traceId, this.componentName, "QOS state provider requested.");

            switch (configuration.MqttQoSStateProvider)
            {
                case MqttServiceConfiguration.QosStateType.AzureStorage:
                    this.logger.Informational(traceId, this.componentName, "QOS state provider requested was Azure Storage.");
                    var azureConfigurationProvider = new ConfigurationProvider<AzureSessionStateConfiguration>(traceId, this.serviceContext.ServiceName, this.serviceContext.CodePackageActivationContext, this.logger, "AzureState");

                    azureConfigurationProvider.ConfigurationChangedEvent += (sender, args) => this.unsupportedConfigurationChangeCallback?.Invoke();
                    azureConfigurationProvider.ConfigurationPropertyChangedEvent += (sender, args) => this.unsupportedConfigurationChangeCallback?.Invoke();

                    AzureSessionStateConfiguration azureConfiguration = azureConfigurationProvider.Config;

                    if (azureConfiguration == null)
                    {
                        this.logger.Critical(traceId, this.componentName, "QOS state provider or configuration could not be retrieved.");
                        throw new ApplicationException("Azure state QoS provider configuration could not be retrieved");
                    }

                    stateProvider = await BlobSessionStatePersistenceProvider.CreateAsync(azureConfiguration.BlobConnectionString, azureConfiguration.ContainerName).ConfigureAwait(false);
                    this.logger.Informational(traceId, this.componentName, "QOS state provider request complete.");
                    break;

                case MqttServiceConfiguration.QosStateType.ServiceFabricStateful:
                    this.logger.Critical(traceId, this.componentName, "QOS state provider requested was Service Fabric Stateful Service but it is not currently implemented.");
                    throw new NotImplementedException("Only azure storage provider is supported");

                default:
                    this.logger.Critical(traceId, this.componentName, "QOS state provider requested was unknown.");
                    throw new ApplicationException("MQTT state provider must be specified in configuration.");
            }


            return stateProvider;
        }

        /// <summary>
        /// Retrieves the state provider for QoS 2 sessions
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlate debugging and diagnostics messages</param>
        /// <param name="configuration">The gateway configuration</param>
        /// <returns>A Qos2 persistence  provider if available</returns>
        async Task<IQos2StatePersistenceProvider> GetQos2StateProvider(Guid traceId, MqttServiceConfiguration configuration)
        {
            IQos2StatePersistenceProvider stateProvider = null;

            this.logger.Informational(traceId, this.componentName, "QOS2 state provider requested.");

            switch (configuration.MqttQoS2StateProvider)
            {
                case MqttServiceConfiguration.QosStateType.AzureStorage:
                    this.logger.Informational(traceId, this.componentName, "QOS2 state provider requested was Azure Storage.");
                    AzureSessionStateConfiguration provider = new ConfigurationProvider<AzureSessionStateConfiguration>(traceId, this.serviceContext.ServiceName,
                                                                                this.serviceContext.CodePackageActivationContext, this.logger, "AzureState")?.Config;
                    if (provider == null)
                    {
                        this.logger.Critical(traceId, this.componentName, "QOS2 state provider or configuration could not be retrieved.");
                        throw new ApplicationException("Azure state QoS2 provider configuration could not be retrieved");
                    }

                    stateProvider = await TableQos2StatePersistenceProvider.CreateAsync(provider.TableConnectionString, provider.TableName).ConfigureAwait(false);
                    this.logger.Informational(traceId, this.componentName, "QOS2 state provider request complete.");
                    break;

                case MqttServiceConfiguration.QosStateType.ServiceFabricStateful:
                    this.logger.Critical(traceId, this.componentName, "QOS2 state provider requested was Service Fabric Stateful Service but it is not currently implemented.");
                    throw new NotImplementedException("Only azure storage provider is supported");

                default:
                    this.logger.Critical(traceId, this.componentName, "QOS2 state provider requested was unknown.");
                    throw new ApplicationException("MQTT state provider must be specified in configuration.");
            }


            return stateProvider;
        }
        #endregion
        #region Certificate Retrieval
        /// <summary>
        /// Retrieves the X509 Certificate used for the server side of TLS
        /// </summary>
        /// <param name="configuration">The gateway configuration</param>
        /// <returns>An X509 Certificate if available</returns>
        X509Certificate2 GetServerCertificate(GatewayConfiguration configuration)
        {
            X509Certificate2 certificate = null;

            switch (configuration.X509Location)
            {
                case GatewayConfiguration.CertificateLocation.Data:
                    string certificateFile = Path.Combine(this.serviceContext.CodePackageActivationContext.GetDataPackageObject("Data").Path, configuration.X509Identifier);
                    certificate = CertificateUtilities.GetCertificateFromFile(certificateFile, configuration.X509Credential);
                    break;

                case GatewayConfiguration.CertificateLocation.KeyVault:
                    //    certificate = CertificateUtilities.GetCertificateFromKeyVault(certificateFile, this.configuration.X509Credential);
                    throw new NotImplementedException();


                case GatewayConfiguration.CertificateLocation.LocalStore:
                    certificate = CertificateUtilities.GetCertificate(configuration.X509Identifier, StoreName.My, StoreLocation.LocalMachine);
                    break;
            }

            return certificate;
        }
        #endregion
        #region Address / URI Construction
        /// <summary>
        /// Retrieves the address that the MQTT listener should listen on
        /// </summary>
        /// <param name="gatewayConfiguration">The Gateway configuration to use when building the address</param>
        /// <returns>A service fabric listening MQTT URI for the requested host and configuration</returns>
        string BuildListeningAddress(GatewayConfiguration gatewayConfiguration)
        {
            return this.BuildAddress(gatewayConfiguration, "+");
        }

        /// <summary>
        /// Retrieves the address that the MQTT listener should publish to service fabric
        /// </summary>
        /// <param name="gatewayConfiguration">The Gateway configuration to use when building the address</param>
        /// <returns>A service fabric publication MQTT URI for the requested host and configuration</returns>
        string BuildPublishAddress(GatewayConfiguration gatewayConfiguration)
        {
            return this.BuildAddress(gatewayConfiguration, FabricRuntime.GetNodeContext().IPAddressOrFQDN);
        }

        /// <summary>
        /// A common MQTT address building method
        /// </summary>
        /// <param name="gatewayConfiguration">The Gateway configuration to use when building the address</param>
        /// <param name="hostName">The name of the host to use</param>
        /// <returns>A MQTT URI for the requested host and configuration</returns>
        string BuildAddress(GatewayConfiguration gatewayConfiguration, string hostName)
        {
            EndpointResourceDescription description = this.serviceContext.CodePackageActivationContext.GetEndpoint(gatewayConfiguration.EndPointName);

            return $"mqtts://{ hostName }:{ description.Port }";
        }
        #endregion
    }
}
