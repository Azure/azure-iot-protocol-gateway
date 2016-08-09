namespace ProtocolGateway.Host.Fabric.FabricShared.Health
{
    #region Using Clauses
    using System;
    using System.Fabric;
    using System.Fabric.Health;
    using System.Threading;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using ProtocolGateway.Host.Fabric.FabricShared.Logging;

    #endregion

    /// <summary>
    /// A basic health reporting class that can be used as a helper with the static methods or a timed reporting service
    /// </summary>
    public sealed class HealthReporter : IDisposable
    {
        #region Delegates
        /// <summary>
        /// A callback method signature used when the reporting service needs feedback on the health of the entity. Implementations of this method should be extremely fast
        /// as it is on a timer with 30 second intervals
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlate debugging and diagnostics entries</param>
        /// <param name="reportSourceId">A unique source for reporting health information</param>
        /// <param name="reportType">The type of the reporting being done (e.g. cluster, instance)</param>
        /// <returns></returns>
        public delegate Dictionary<string, HealthState> ReportGenerator(Guid traceId, string reportSourceId, ReportTypes reportType);
        #endregion
        #region Variables
        /// <summary>
        /// Tracks the number of times the type has been disposed
        /// </summary>
        int disposalCount;

        /// <summary>
        /// The entity type the health report is for
        /// </summary>
        readonly ReportTypes reportType;

        /// <summary>
        /// A unique id used to represent the source of the health report
        /// </summary>
        readonly string reportSourceId;

        /// <summary>
        /// A method to be called when the health data is required
        /// </summary>
        readonly ReportGenerator reportCallback;

        /// <summary>
        /// The Service Fabric client used to report the health data
        /// </summary>
        readonly FabricClient client;

        /// <summary>
        /// A timer used to call the method for health reporting
        /// </summary>
        Timer timer;

        /// <summary>
        /// The interval to delay between health reports
        /// </summary>
        readonly TimeSpan interval;

        /// <summary>
        /// The service context
        /// </summary>
        readonly ServiceContext context;

        /// <summary>
        /// The time to live for each health report
        /// </summary>
        readonly TimeSpan timeToLive;

        /// <summary>
        /// A synchronization control to make sure that start and stop commands are not called at the same time
        /// </summary>
        readonly SemaphoreSlim lifecycleControl= new SemaphoreSlim(1);

        /// <summary>
        /// An instance used to write debugging and diagnostics information
        /// </summary>
        readonly ILogger logger;

        /// <summary>
        /// The name of the component for debugging and diagnostics messages
        /// </summary>
        readonly string componentName;
        #endregion
        #region Constructors
        /// <summary>
        /// Initializes a new instance of the reporting type and 
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlate the debugging and diagnostics messages</param>
        /// <param name="logger">An instance used to write debugging and diagnostics information</param>
        /// <param name="componentName">The name of the component for debugging and diagnostics messages</param>
        /// <param name="reportSourceId">A unique id used to represent the source of the health report</param>
        /// <param name="reportCallback">A method to be called when the health data is required</param>
        /// <param name="context">The service fabric context that is assocaited with the entity being reported on</param>
        /// <param name="reportType">The entity type the health report is for</param>
        /// <param name="reportingInterval">How often the report will be sent</param>
        /// <param name="batchInterval">The amount of time to delay before sending for batching purposes, 0 for immediate</param>
        /// <param name="timeout">The timeout for sending a report</param>
        /// <param name="retryInterval">The amount of time to wait before trying to resend a report</param>
        public HealthReporter(Guid traceId, ILogger logger, string componentName, string reportSourceId, ReportGenerator reportCallback, ServiceContext context, ReportTypes reportType, TimeSpan? reportingInterval = null, TimeSpan? batchInterval = null, TimeSpan? timeout = null, TimeSpan? retryInterval = null)
        {
            if (string.IsNullOrEmpty(reportSourceId)) throw new ArgumentException("Parameter cannot be null or empty.", nameof(reportSourceId));
            if (reportCallback == null) throw new ArgumentNullException(nameof(reportCallback));

            this.logger = logger;
            this.componentName = componentName;
            this.logger.Informational(traceId, this.componentName, "Instantiated health reporter");


            this.reportSourceId = reportSourceId;
            this.reportType = reportType;
            this.reportCallback = reportCallback;

            this.client = new FabricClient(new FabricClientSettings
            {
                HealthReportRetrySendInterval = retryInterval ?? TimeSpan.FromSeconds(40),
                HealthReportSendInterval = batchInterval ?? TimeSpan.FromSeconds(0),
                HealthOperationTimeout = timeout ?? TimeSpan.FromSeconds(120)
            });
            
            this.context = context;

            this.interval = reportingInterval ?? TimeSpan.FromSeconds(30);
            if (this.interval < TimeSpan.FromSeconds(5)) this.interval = TimeSpan.FromSeconds(15);

            this.timeToLive = TimeSpan.FromSeconds((this.interval.TotalSeconds * 2.0) + 1.0);
        }
        #endregion
        #region Lifecycle
        /// <summary>
        /// Starts submitting reports at the identified interval
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlate the debugging and diagnostics messages</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests</param>
        public async Task StartAsync(Guid traceId, CancellationToken? cancellationToken = null)
        {
            this.logger.Informational(traceId, this.componentName, "StartAsync() invoked");

            if (this.timer == null && this.reportType > 0 && await this.lifecycleControl.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken ?? CancellationToken.None).ConfigureAwait(false))
            {
                try
                {
                    if (this.reportType > 0) this.timer = new Timer(this.TimerCallback, null, this.interval, this.interval);
                }
                finally
                {
                    this.lifecycleControl.Release();
                }
            }
        }

        /// <summary>
        /// Stops sending reports
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlate the debugging and diagnostics messages</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests</param>
        public async Task StopAsync(Guid traceId, CancellationToken? cancellationToken = null)
        {
            this.logger.Informational(traceId, this.componentName, "StopAsync() invoked");

            if (this.timer != null && await this.lifecycleControl.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken ?? CancellationToken.None).ConfigureAwait(false))
            {
                try
                {
                    this.timer.Dispose();
                    this.timer = null;
                }
                finally
                {
                    this.lifecycleControl.Release();
                }
            }
        }
        #endregion
        #region Safe Disposal Pattern
        /// <summary>
        /// A finalizer used to guarantee the instance has been disposed of
        /// </summary>
        ~HealthReporter()
        {
            this.Dispose(false);
        }

        /// <summary>
        /// Provides a way for the client to indicate it is done with the resource
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
        }

        /// <summary>
        /// A common dispose method for internal use
        /// </summary>
        /// <param name="isDisposing">True if the method was called from the Dispose() method</param>
        void Dispose(bool isDisposing)
        {
            if (Interlocked.Increment(ref this.disposalCount) == 1)
            {
                if (isDisposing) GC.SuppressFinalize(this);
                this.timer.Dispose();
                this.client.Dispose();
            }
        }
        #endregion
        #region Instance Methods
        /// <summary>
        /// Timer callback to report state
        /// </summary>
        /// <param name="state">A state object that is not used by this implementation</param>
        void TimerCallback(object state)
        {
            Guid traceId = Guid.NewGuid();

            this.logger.Verbose(traceId, this.componentName, "Health report requested");

            foreach (object reportValue in Enum.GetValues(typeof(ReportTypes)))
            {
                if (this.disposalCount < 1)
                {
                    var currentReportType = (ReportTypes)((int)this.reportType & (int)reportValue);

                    if (currentReportType != ReportTypes.None)
                    {
                        IDictionary<string, HealthState> reports = this.reportCallback(traceId, this.reportSourceId, currentReportType);

                        if (reports != null)
                        {
                            foreach (KeyValuePair<string, HealthState> reportedState in reports)
                            {
                                HealthReport information = GetHealthReport(this.context, this.reportSourceId, reportedState.Key, reportedState.Value, currentReportType, this.timeToLive);
                                this.client.HealthManager.ReportHealth(information);
                            }
                        }
                    }
                }
            }
        }
        #endregion
        #region Static Methods
        /// <summary>
        /// Sends a health report in using a service fabric client for immediate transmission (no batching)
        /// </summary>
        /// <param name="report">The health report to send</param>
        /// <param name="batchDelay">The maximum amount of time to wait before sending in an effort to batch transmission</param>
        /// <param name="timeout">The time to wait before considering the send as timed out</param>
        /// <param name="retryInterval">The amount of time to wait before sending the report again on failure</param>
        public static void SubmitHealthReport(HealthReport report, TimeSpan? batchDelay = null, TimeSpan? timeout = null, TimeSpan? retryInterval = null)
        {
            var client = new FabricClient(new FabricClientSettings
            {
                HealthReportSendInterval = batchDelay ?? TimeSpan.FromSeconds(0),
                HealthOperationTimeout = timeout ?? TimeSpan.FromSeconds(15),
                HealthReportRetrySendInterval = retryInterval ?? TimeSpan.FromSeconds(50)
            });

            client.HealthManager.ReportHealth(report);
        }

        /// <summary>
        /// Returns a health report
        /// </summary>
        /// <param name="context">The service fabric context that the health report is for</param>
        /// <param name="reportSourceId">The unique reporting source id</param>
        /// <param name="propertyName">The name of the health property being reported on</param>
        /// <param name="state">The current state of the health property</param>
        /// <param name="timeToLive">The time to live of the health report</param>
        /// <param name="reportType">The entity type the report is for</param>
        /// <returns>A health report for the appropriate reporting entity</returns>
        public static HealthReport GetHealthReport(ServiceContext context, string reportSourceId, string propertyName, HealthState state, ReportTypes reportType, TimeSpan timeToLive)
        {
            HealthReport report;
            var information = new HealthInformation(reportSourceId, propertyName, state);

            information.Description = $"{ propertyName } health state { Enum.GetName(typeof(HealthState), state) }";
            information.RemoveWhenExpired = true;
            information.TimeToLive = timeToLive;
            information.SequenceNumber = HealthInformation.AutoSequenceNumber;

            switch (reportType)
            {
                case ReportTypes.Cluster:
                    report = new ClusterHealthReport(information);
                    break;

                case ReportTypes.Application:
                    report = new ApplicationHealthReport(new Uri(context.CodePackageActivationContext.ApplicationName), information);
                    break;

                case ReportTypes.DeployedApplication:
                    report = new DeployedApplicationHealthReport(new Uri(context.CodePackageActivationContext.ApplicationName), context.NodeContext.NodeName, information);
                    break;

                case ReportTypes.Service:
                    report = new ServiceHealthReport(context.ServiceName, information);
                    break;

                case ReportTypes.DeployedService:
                    report = new DeployedServicePackageHealthReport(new Uri(context.CodePackageActivationContext.ApplicationName), context.CodePackageActivationContext.GetServiceManifestName(), context.NodeContext.NodeName, information);
                    break;

                case ReportTypes.Node:
                    report = new NodeHealthReport(context.NodeContext.NodeName, information);
                    break;

                case ReportTypes.Instance:
                    if (context is StatelessServiceContext)
                    {
                        report = new StatelessServiceInstanceHealthReport(context.PartitionId, context.ReplicaOrInstanceId, information);
                    }
                    else
                    {
                        report = new StatefulServiceReplicaHealthReport(context.PartitionId, context.ReplicaOrInstanceId, information);
                    }
                    break;

                default:
                    throw new ArgumentException("Unknown health type", nameof(reportType));
            }

            return report;
        }
        #endregion
        #region Enumerations
        /// <summary>
        /// The type of reporting entities
        /// </summary>
        [Flags]
        public enum ReportTypes
        {
            None = 0,

            /// <summary>
            /// Instance of stateful or stateless service
            /// </summary>
            Instance = 1,

            /// <summary>
            /// The full service cross instance and deployment
            /// </summary>
            Service = 2,

            /// <summary>
            /// The full application cross instance and deployment
            /// </summary>
            Application = 4,

            /// <summary>
            /// The cluster
            /// </summary>
            Cluster = 8,

            /// <summary>
            /// A single deployment of the application
            /// </summary>
            DeployedApplication = 16,

            /// <summary>
            /// A single deployment of the service
            /// </summary>
            DeployedService = 32,

            /// <summary>
            /// The entire node
            /// </summary>
            Node = 64
        }
        #endregion
    }
}
