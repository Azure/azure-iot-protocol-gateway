namespace ProtocolGateway.Host.Fabric.FrontEnd
{
    #region Using Clauses
    using System;
    using System.Collections.Generic;
    using System.Fabric;
    using System.Fabric.Health;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.ProtocolGateway.Instrumentation;
    using Microsoft.ServiceFabric.Services.Communication.Runtime;
    using Microsoft.ServiceFabric.Services.Runtime;
    using FabricShared.Configuration;
    using FabricShared.Logging;
    using Configuration;
    using Logging;
    using Mqtt;
    using ProtocolGateway.Host.Fabric.FabricShared.Health;

    #endregion

    /// <summary>
    /// An instance of this class is created for each service instance by the Service Fabric runtime.
    /// </summary>
    sealed class FrontEnd : StatelessService
    {
        #region Constants
        /// <summary>
        /// The name of the component for debugging and diagnostics
        /// </summary>
        const string ComponentName = @"Stateless Front End";
        #endregion
        #region Variables
        /// <summary>
        /// The logger used to write debugging and diagnostics messages
        /// </summary>
        readonly IServiceLogger logger;

        /// <summary>
        /// A configuration provider used to store the gateway configuration elements
        /// </summary>
        readonly IConfigurationProvider<GatewayConfiguration> configurationProvider;

        /// <summary>
        /// A unique id used for reporting health status
        /// </summary>
        readonly string healthReporterId = Guid.NewGuid().ToString("N");

        /// <summary>
        /// True if an unsupported configuration change has occurred requiring the service to reinitialize the communication stack
        /// </summary>
        bool unsupportedConfigurationChangeOccurred;

        /// <summary>
        /// The reporter used to notify the fabric of the current capacity values
        /// </summary>
        MetricReporter metricReporter;
        #endregion
        #region Constructors
        /// <summary>
        /// Initializes a new instance of the front end service for the protocol gateway
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlate debugging and diagnostics messages</param>
        /// <param name="systemName">The name of the system used for debugging and diagnostics</param>
        /// <param name="context">The service fabric service context</param>
        public FrontEnd(Guid traceId, string systemName, StatelessServiceContext context)
            : base(context)
        {
            this.logger = new ServiceLogger(systemName, context);
            this.logger.ServiceInstanceConstructed(traceId, ComponentName, this.GetType().FullName);

            // Optimizations added for Azure Storage REST
            ServicePointManager.DefaultConnectionLimit = Environment.ProcessorCount * 12;
            ServicePointManager.UseNagleAlgorithm = false;
            ServicePointManager.Expect100Continue = false;

            this.configurationProvider = new ConfigurationProvider<GatewayConfiguration>(traceId, context.ServiceName, context.CodePackageActivationContext, this.logger);
        }
        #endregion
        #region Service Lifecycle
        /// <summary>
        /// Optional override to create listeners (e.g., TCP, HTTP) for this service replica to handle client or user requests.
        /// </summary>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            Guid traceId = Guid.NewGuid();

            this.logger.Verbose(traceId, ComponentName, "CreateServiceInstanceListeners() Invoked.");

            return new[]
            {
                new ServiceInstanceListener(serviceContext => new MqttCommunicationListener(traceId, ComponentName, serviceContext, this.logger, this.configurationProvider, this.OnUnsupportedConfigurationChange))
            };
        }

        /// <summary>
        /// Called when the Service Fabric is opening the service (part of the standard life cycle)
        /// </summary>
        /// <param name="cancellationToken">A token to monitor for cancellation</param>
        /// <returns>A task that completes when the service is open</returns>
        protected override async Task OnOpenAsync(CancellationToken cancellationToken)
        {
            Guid traceId = Guid.NewGuid();

            this.logger.OpenAsyncInvoked(traceId, ComponentName, this.GetType().Name);

            this.metricReporter?.Dispose();
            this.metricReporter = new MetricReporter(traceId, this.logger, ComponentName, this.Context, this.Partition, this.MetricsCallback, TimeSpan.FromSeconds(15));

            await this.metricReporter.StartAsync(traceId, cancellationToken).ConfigureAwait(false);
            await base.OnOpenAsync(cancellationToken).ConfigureAwait(false);
        }


        /// <summary>
        /// Called when the Service Fabric is aborting the current service (part of the standard life cycle)
        /// </summary>
        protected override void OnAbort()
        {
            Guid traceId = Guid.NewGuid();

            this.logger.AbortInvoked(traceId, ComponentName, this.GetType().Name);

            using (var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
            {
                try
                {
                    this.metricReporter.StopAsync(traceId, tokenSource.Token).GetAwaiter().GetResult();
                }
                catch (Exception e)
                {
                    this.logger.Error(traceId, ComponentName, "Error stopping the metrics reporter or timed out", null, e);
                }    

                this.metricReporter.Dispose();
                this.metricReporter = null;
            }

            base.OnAbort();
        }

        /// <summary>
        /// Called when the Service Fabric is closing the service down (part of the standard life cycle)
        /// </summary>
        /// <param name="cancellationToken">A token to monitor for cancellation</param>
        /// <returns>A task that completes when the service is closed</returns>
        protected override async Task OnCloseAsync(CancellationToken cancellationToken)
        {
            Guid traceId = Guid.NewGuid();

            this.logger.CloseAsyncInvoked(traceId, ComponentName, this.GetType().Name);

            if (this.metricReporter != null)
            {
                await this.metricReporter.StopAsync(traceId, cancellationToken).ConfigureAwait(false);
                this.metricReporter.Dispose();
                this.metricReporter = null;
            }

            await base.OnCloseAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// This is the main entry point for your service instance.
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service instance.</param>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            Guid traceId = Guid.NewGuid();

            using (var healthReporter = new HealthReporter(traceId, this.logger, ComponentName, this.healthReporterId, this.HealthReportCallback, this.Context, HealthReporter.ReportTypes.Instance, this.configurationProvider.Config.HealthReportInterval))
            {
                await healthReporter.StartAsync(traceId, cancellationToken).ConfigureAwait(false);
                this.logger.RunAsyncInvoked(traceId, ComponentName, this.GetType().FullName);
                
                while (!cancellationToken.IsCancellationRequested && !this.unsupportedConfigurationChangeOccurred)
                {
                    await Task.Delay(250, cancellationToken).ConfigureAwait(false);
                }

                await healthReporter.StopAsync(traceId, cancellationToken).ConfigureAwait(false);
            }

            this.logger.Informational(traceId, ComponentName, "RunAsync completed.");
        }
        #endregion
        #region Health and Capacity Metrics
        /// <summary>
        /// Called to collect capactity metrics for the service
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlate debug and diagnostics records</param>
        /// <param name="reportedMetrics">A list of metrics that are configured in the service</param>
        /// <returns>An enumeration of key value pairs that are used as the source of the capacity reports (the capacity / load numbers for each metric)</returns>
        IEnumerable<KeyValuePair<string, int>> MetricsCallback(Guid traceId, string[] reportedMetrics)
        {
            this.logger.Verbose(traceId, ComponentName, "Reporting metrics");

            return new[]
            {
                new KeyValuePair<string, int>("ConnectionsCurrent", Convert.ToInt32(PerformanceCounters.ConnectionsCurrent.RawValue)),
                new KeyValuePair<string, int>("MessagesReceivedPerSecond", Convert.ToInt32(PerformanceCounters.MessagesReceivedPerSecond.NextValue)),
                new KeyValuePair<string, int>("MessagesSentPerSecond", Convert.ToInt32(PerformanceCounters.MessagesSentPerSecond.NextValue))
            };
        }

        /// <summary>
        /// A method that is called when an unsupported configuration change occurs.
        /// </summary>
        void OnUnsupportedConfigurationChange()
        {
            this.unsupportedConfigurationChangeOccurred = true;
        }


        /// <summary>
        /// Gets a list of health reports
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlated debugging and dignostics logs</param>
        /// <param name="reportSourceId">The source id of the reporter</param>
        /// <param name="reportType">The type of reports to be generated</param>
        /// <returns>A dictionary of health reports</returns>
        Dictionary<string, HealthState> HealthReportCallback(Guid traceId, string reportSourceId, HealthReporter.ReportTypes reportType)
        {
            var states = new Dictionary<string, HealthState>();

            this.logger.Verbose(traceId, ComponentName, "Reporting health");

            if ((reportType & HealthReporter.ReportTypes.Instance) == HealthReporter.ReportTypes.Instance)
            {
                int connectionsFailedPerSecond = Convert.ToInt32(PerformanceCounters.ConnectionFailedOperationalPerSecond.NextValue);
                HealthState connectionFailedState;

                if (connectionsFailedPerSecond < 50)
                {
                    connectionFailedState = HealthState.Ok;
                }
                else if (connectionsFailedPerSecond < 100)
                {
                    connectionFailedState = HealthState.Warning;
                    this.logger.Warning(traceId, ComponentName, "Health: High failed connections per second '{0}'", new object[] { connectionsFailedPerSecond });
                }
                else
                {
                    connectionFailedState = HealthState.Error;
                    this.logger.Error(traceId, ComponentName, "Health: Very high failed connections per second '{0}'", new object[] { connectionsFailedPerSecond });
                }

                states.Add("ConnectionFailedOperationalPerSecond", connectionFailedState);
            }

            return states;
        }
        #endregion
    }
}
