namespace ProtocolGateway.Host.FabricSetup.CounterSetup
{
    #region Using Clauses
    using System;
    using Microsoft.Azure.Devices.ProtocolGateway.Instrumentation;
    using logging;
    #endregion

    /// <summary>
    /// Used to register performance counters and the appropriate category for the service fabric front end service
    /// </summary>
    class Program
    {
        /// <summary>
        /// Primary entry point for the program
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            const string ComponentName = @"Front End Setup";

            var logger = new StartupLogger("Protocol Gateway Setup");
            Guid traceId = Guid.NewGuid();

            try
            {
                logger.Informational(traceId, ComponentName, "Launched.");
                PerformanceCounters.RegisterCountersIfRequired();
                logger.Informational(traceId, ComponentName, "Completed without exceptions.");
            }
            catch (Exception e)
            {
                logger.Critical(traceId, ComponentName, "Could not prepare the environment due to an error.", null, e);
                throw;
            }
        }
    }
}
