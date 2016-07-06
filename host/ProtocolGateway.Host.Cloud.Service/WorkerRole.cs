// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ProtocolGateway.Host.Cloud.Service
{
    using System;
    using System.Diagnostics;
    using System.Diagnostics.Tracing;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Internal.Logging;
    using Microsoft.Azure.Devices.ProtocolGateway;
    using Microsoft.Azure.Devices.ProtocolGateway.Instrumentation;
    using Microsoft.Azure.Devices.ProtocolGateway.Providers.CloudStorage;
    using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;
    using Microsoft.WindowsAzure.ServiceRuntime;
    using ProtocolGateway.Host.Common;

    public class WorkerRole : RoleEntryPoint
    {
        readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        readonly ManualResetEvent runCompleteEvent = new ManualResetEvent(false);

        public override void Run()
        {
            Trace.TraceInformation("Contoso.ProtocolGateway.Cloud.Host is running");

            try
            {
                this.RunAsync(this.cancellationTokenSource.Token).Wait();
            }
            finally
            {
                this.runCompleteEvent.Set();
            }
        }

        public override bool OnStart()
        {
            // For information on handling configuration changes
            // see the MSDN topic at http://go.microsoft.com/fwlink/?LinkId=166357.

            bool result = base.OnStart();

            Trace.TraceInformation("Contoso.ProtocolGateway.Cloud.Host has been started");

            return result;
        }

        public override void OnStop()
        {
            Trace.TraceInformation("Contoso.ProtocolGateway.Cloud.Host is stopping");

            this.cancellationTokenSource.Cancel();
            this.runCompleteEvent.WaitOne();

            base.OnStop();

            Trace.TraceInformation("Contoso.ProtocolGateway.Cloud.Host has stopped");
        }

        async Task RunAsync(CancellationToken cancellationToken)
        {
            var settingsProvider = new RoleEnvironmentSettingsProvider();

            var eventListener = new ObservableEventListener();
            eventListener.LogToWindowsAzureTable(RoleEnvironment.CurrentRoleInstance.Id, settingsProvider.GetSetting("BlobSessionStatePersistenceProvider.StorageConnectionString"), bufferingInterval: TimeSpan.FromMinutes(2));
            eventListener.EnableEvents(BootstrapperEventSource.Log, EventLevel.Informational);
            eventListener.EnableEvents(CommonEventSource.Log, EventLevel.Informational);
            eventListener.EnableEvents(DefaultEventSource.Log, EventLevel.Informational);
            
            int minWorkerThreads;
            int minCompletionPortThreads;
            ThreadPool.GetMinThreads(out minWorkerThreads, out minCompletionPortThreads);
            ThreadPool.SetMinThreads(minWorkerThreads, Math.Max(16, minCompletionPortThreads));

            int threadCount = Environment.ProcessorCount;
            
            BlobSessionStatePersistenceProvider blobSessionStateProvider =
                await BlobSessionStatePersistenceProvider.CreateAsync(
                    settingsProvider.GetSetting("BlobSessionStatePersistenceProvider.StorageConnectionString"),
                    settingsProvider.GetSetting("BlobSessionStatePersistenceProvider.StorageContainerName"));

            TableQos2StatePersistenceProvider tableQos2StateProvider =
                await TableQos2StatePersistenceProvider.CreateAsync(
                    settingsProvider.GetSetting("TableQos2StatePersistenceProvider.StorageConnectionString"),
                    settingsProvider.GetSetting("TableQos2StatePersistenceProvider.StorageTableName"));

            var bootstrapper = new Bootstrapper(
                settingsProvider,
                blobSessionStateProvider,
                tableQos2StateProvider);

            X509Certificate2 tlsCertificate = GetTlsCertificate(settingsProvider.GetSetting("TlsCertificateThumbprint"),
                StoreName.My, StoreLocation.LocalMachine);

            try
            {
                await bootstrapper.RunAsync(tlsCertificate, threadCount, cancellationToken);
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
                throw;
            }
            await bootstrapper.CloseCompletion;
        }

        public static X509Certificate2 GetTlsCertificate(string thumbprint, StoreName storeName, StoreLocation storeLocation)
        {
            X509Store store = null;
            try
            {
                store = new X509Store(storeName, storeLocation);
                store.Open(OpenFlags.ReadOnly);
                X509Certificate2Collection certCollection = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);

                return certCollection.Count == 0 ? null : certCollection[0];
            }
            finally
            {
                if (store != null)
                {
                    store.Close();
                }
            }
        }
    }
}