namespace ProtocolGateway.Host.Fabric.BackEnd
{
    using System;
    using System.Diagnostics.Tracing;
    using System.Threading;

    using Microsoft.Azure.Devices.ProtocolGateway.Instrumentation;
    using Microsoft.ServiceFabric.Services.Runtime;
    using ProtocolGateway.Host.Fabric.FabricShared.Logging;

    internal static class Program
    {
        /// <summary>
        /// This is the entry point of the service host process.
        /// </summary>
        private static void Main()
        {
            const string SystemName = @"Protocol Gateway Back End";
            const string ComponentName = @"Registration Application";

            ILogger logger = new Logger(SystemName);
            Guid traceId = Guid.NewGuid();

            try
            {
                // The ServiceManifest.XML file defines one or more service type names.
                // Registering a service maps a service type name to a .NET type.
                // When Service Fabric creates an instance of this service type,
                // an instance of the class is created in this host process.

                logger.Informational(traceId, ComponentName, "Initializing BackEndType.");
                ServiceRuntime.RegisterServiceAsync("BackEndType", context => new BackEnd(traceId, SystemName, context)).GetAwaiter().GetResult();
                logger.Informational(traceId, ComponentName, "Services registered.");

                // Prevents this host process from terminating so services keep running.
                Thread.Sleep(Timeout.Infinite);
            }
            catch (Exception e)
            {
                logger.Critical(traceId, ComponentName, "Error during registration of type {0}.", new object[] { typeof(BackEnd).Name }, e);
                throw;
            }
        }
    }
}
