namespace ProtocolGateway.Host.Fabric.FrontEnd
{
    #region Using Clauses
    using System;
    using System.Threading;
    using Microsoft.ServiceFabric.Services.Runtime;
    using FabricShared.Logging;
    using Logging;
    #endregion

    /// <summary>
    /// Host application to register service fabric types
    /// </summary>
    static class Program
    {
        /// <summary>
        /// This is the entry point of the service host process.
        /// </summary>
        static void Main()
        {
            const string SystemName = @"Protocol Gateway Front End";
            const string ComponentName = @"Registration Application";

            ILogger logger = new Logger(SystemName);
            Guid traceId = Guid.NewGuid();

            try
            {
                logger.Informational(traceId, ComponentName, "Initializing FrontEndType.");
                ServiceRuntime.RegisterServiceAsync("FrontEndType", context =>  new FrontEnd(traceId, SystemName, context)).GetAwaiter().GetResult();
                logger.Informational(traceId, ComponentName, "Services registered.");

                // Prevents this host process from terminating so services keep running.
                Thread.Sleep(Timeout.Infinite);
            }
            catch (Exception e)
            {
                logger.Critical(traceId, ComponentName, "Error during registration of type {0}.", new object[] { typeof(FrontEnd).Name }, e);
                throw;
            }
        }
    }
}
