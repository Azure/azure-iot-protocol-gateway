namespace ProtocolGateway.Host.Fabric.FabricShared.Health
{
    #region Using Clauses
    using System;
    using System.Collections.Generic;
    using System.Fabric;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using ProtocolGateway.Host.Fabric.FabricShared.Logging;

    #endregion

    /// <summary>
    /// A basic health reporting class that can be used as a helper with the static methods or a timed reporting service
    /// </summary>
    public sealed class MetricReporter : IDisposable
    {
        #region Delegates
        /// <summary>
        /// Returns an enumeration of metrics for the provided metric names
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlate debugging and diagnostics entries</param>
        /// <param name="reportedMetrics">The name of the metrics to report on</param>
        /// <returns>An enumeration containing the metrics to report</returns>
        public delegate IEnumerable<KeyValuePair<string, int>> GetMetrics(Guid traceId, string[] reportedMetrics);
        #endregion
        #region Variables
        /// <summary>
        /// Tracks the number of times the type has been disposed
        /// </summary>
        int disposalCount;

        /// <summary>
        /// A timer used to call the method for health reporting
        /// </summary>
        Timer timer;

        /// <summary>
        /// The interval to delay between health reports
        /// </summary>
        readonly TimeSpan interval;

        /// <summary>
        /// The report metrics that have been configured
        /// </summary>
        readonly string[] reportMetrics;

        /// <summary>
        /// The partition of the service the metrics are being reported for
        /// </summary>
        readonly IServicePartition partition;

        /// <summary>
        /// A synchronization control to make sure that start and stop commands are not called at the same time
        /// </summary>
        readonly SemaphoreSlim lifecycleControl = new SemaphoreSlim(1);

        /// <summary>
        /// The callback method to call for reporting
        /// </summary>
        readonly GetMetrics metricsCallback;

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
        /// Initializes a new instance of the metrics type 
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlate the debugging and diagnostics messages</param>
        /// <param name="logger">An instance used to write debugging and diagnostics information</param>
        /// <param name="componentName">The name of the component for debugging and diagnostics messages</param>
        /// <param name="context">The service fabric context that is assocaited with the entity being reported on</param>
        /// <param name="partition">The partition of the service the metrics are being reported for</param>
        /// <param name="metricsCallback">The method that is called to report metrics</param>
        /// <param name="reportingInterval">How often the report will be sent</param>
        public MetricReporter(Guid traceId, ILogger logger, string componentName, ServiceContext context, IServicePartition partition, GetMetrics metricsCallback, TimeSpan? reportingInterval = null)
        {
            this.logger = logger;
            this.componentName = componentName;

            this.logger.Informational(traceId, this.componentName, "Instantiated metrics reporter");

            this.interval = reportingInterval ?? TimeSpan.FromSeconds(30);
            if (this.interval < TimeSpan.FromSeconds(5)) this.interval = TimeSpan.FromSeconds(15);


            this.metricsCallback = metricsCallback;
            this.reportMetrics = context.CodePackageActivationContext.GetServiceTypes()[context.ServiceTypeName].LoadMetrics?.Select(item => item.Name).ToArray();
            this.partition = partition;
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

            if (this.timer == null && await this.lifecycleControl.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken ?? CancellationToken.None).ConfigureAwait(false))
            {
                try
                {
                    this.timer = new Timer(this.ReportLoad, null, this.interval, this.interval);
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
        #region Metrics Reporting
        /// <summary>
        /// Submits the load and metrics information
        /// </summary>
        /// <param name="state">The state from the callback</param>
        void ReportLoad(object state)
        {
            Guid traceId = Guid.NewGuid();

            this.logger.Verbose(traceId, this.componentName, "ReportLoad() invoked");

            if (this.disposalCount < 1)
            {
                if ((this.metricsCallback != null) && (this.reportMetrics != null) && (this.reportMetrics.Length > 0))
                {
                    try
                    {
                        KeyValuePair<string, int>[] metrics = this.metricsCallback(traceId, this.reportMetrics).ToArray();

                        if (metrics.Length > 0)
                        {
                            this.partition.ReportLoad(metrics.Select(item => new LoadMetric(item.Key, item.Value)));
                        }
                    }
                    catch (FabricObjectClosedException e)
                    {
                        this.logger.Error(traceId, this.componentName, "Error reporting metrics", null, e);
                    }
                }
            }
            else
            {
                this.logger.Verbose(traceId, this.componentName, "No metrics configured");
            }
        }
        #endregion
        #region Safe Disposal Pattern
        /// <summary>
        /// A finalizer used to guarantee the instance has been disposed of
        /// </summary>
        ~MetricReporter()
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
            }
        }
        #endregion
    }
}
