// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ProtocolGateway.Host.Kube
{
    using System;
    using System.IO;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.ProtocolGateway;
    using Microsoft.Azure.Devices.ProtocolGateway.Providers.CloudStorage;
    using Microsoft.Diagnostics.EventFlow;
    using Microsoft.Extensions.Configuration;
    using ProtocolGateway.Host.Common;

    class Program
    {
        static void Main(string[] args)
        {
            // optimizing IOCP performance
            int minWorkerThreads;
            int minCompletionPortThreads;
            ThreadPool.GetMinThreads(out minWorkerThreads, out minCompletionPortThreads);
            ThreadPool.SetMinThreads(minWorkerThreads, Math.Max(16, minCompletionPortThreads));

            int threadCount = Environment.ProcessorCount;
            if (args.Length > 0)
            {
                threadCount = int.Parse(args[0]);
            }

            using (var diagnosticsPipeline = DiagnosticPipelineFactory.CreatePipeline("eventFlowConfig.json"))
            {
                var cts = new CancellationTokenSource();

                var certificate = new X509Certificate2(Path.Combine(AppContext.BaseDirectory, "protocol-gateway.contoso.com.pfx"), "password");
                var config = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
                var settingsProvider = new ConfigurationSettingsProvider(config.GetSection("AppSettings"));
                BlobSessionStatePersistenceProvider blobSessionStateProvider = BlobSessionStatePersistenceProvider.CreateAsync(
                    settingsProvider.GetSetting("BlobSessionStatePersistenceProvider.StorageConnectionString"),
                    settingsProvider.GetSetting("BlobSessionStatePersistenceProvider.StorageContainerName")).Result;

                TableQos2StatePersistenceProvider tableQos2StateProvider = TableQos2StatePersistenceProvider.CreateAsync(
                    settingsProvider.GetSetting("TableQos2StatePersistenceProvider.StorageConnectionString"),
                    settingsProvider.GetSetting("TableQos2StatePersistenceProvider.StorageTableName")).Result;

                var bootstrapper = new Bootstrapper(settingsProvider, blobSessionStateProvider, tableQos2StateProvider);
                Task.Run(() => bootstrapper.RunAsync(certificate, threadCount, cts.Token), cts.Token);

                while (true)
                {
                    string input = Console.ReadLine();
                    if (input != null && input.Equals("exit", StringComparison.InvariantCultureIgnoreCase))
                    {
                        break;
                    }
                }

                cts.Cancel();
                bootstrapper.CloseCompletion.Wait(TimeSpan.FromSeconds(20));
            }
        }
    }
}
