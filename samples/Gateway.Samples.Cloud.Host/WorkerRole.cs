// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Gateway.Samples.Cloud.Host
{
    using System;
    using System.Diagnostics;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using Gateway.Samples.Common;
    using Microsoft.Azure.Devices.Gateway.Cloud;
    using Microsoft.Azure.Devices.Gateway.Core;
    using Microsoft.WindowsAzure.ServiceRuntime;

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
            // optimizing IOCP performance
            int minWorkerThreads;
            int minCompletionPortThreads;
            ThreadPool.GetMinThreads(out minWorkerThreads, out minCompletionPortThreads);
            ThreadPool.SetMinThreads(minWorkerThreads, Math.Max(16, minCompletionPortThreads));

            int threadCount = Environment.ProcessorCount;

            var settingsProvider = new SettingsProvider();
            X509Certificate2 tlsCertificate = GetTlsCertificate(settingsProvider.GetSetting("TlsCertificateThumbprint"), StoreName.My, StoreLocation.LocalMachine);
            BlobSessionStateManager blobSessionStateManager = await BlobSessionStateManager.CreateAsync(
                settingsProvider.GetSetting("SessionStateManager.StorageConnectionString"),
                settingsProvider.GetSetting("SessionStateManager.StorageContainerName"));

            var bootstrapper = new Bootstrapper(
                settingsProvider,
                blobSessionStateManager);
            await bootstrapper.RunAsync(tlsCertificate, threadCount, cancellationToken);
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