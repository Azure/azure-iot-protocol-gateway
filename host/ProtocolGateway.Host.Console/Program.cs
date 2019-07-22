// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ProtocolGateway.Host.Console.NetStandard
{
    using System;
    using System.IO;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.ProtocolGateway;
    using ProtocolGateway.Host.Common;
    using Microsoft.Azure.Devices.ProtocolGateway.Providers.CloudStorage;
    using Microsoft.Diagnostics.EventFlow;
    using Microsoft.Extensions.Configuration;
    using CommandLine;

    class Program
    {
        public class Options
        {
            [Option('c', "config", Required = false, HelpText = "Set location of configuration file.")]
            public string ConfigPath { get; set; }

            [Option('e', "env", Required = false, HelpText = "Use environment variables as configuration source.")]
            public bool UseEnvironment { get; set; }
        }

        static void Main(string[] args)
        {
            // optimizing IOCP performance
            int minWorkerThreads;
            int minCompletionPortThreads;
            ThreadPool.GetMinThreads(out minWorkerThreads, out minCompletionPortThreads);
            ThreadPool.SetMinThreads(minWorkerThreads, Math.Max(16, minCompletionPortThreads));

            CommandLine.Parser.Default.ParseArguments<Options>(args)
                .WithParsed(opts => Run(opts));
        }

        static void Run(Options options)
        {
            var config = new ConfigurationBuilder().AddJsonFile(options.ConfigPath ?? "appSettings.json");
            if (options.UseEnvironment)
            {
                config.AddEnvironmentVariables("ProtocolGateway.");
            }
            var settingsProvider = new ConfigurationSettingsProvider(config.Build());

            int threadCount = Environment.ProcessorCount;
            if (settingsProvider.TryGetIntegerSetting("WorkerCount", out int workers))
            {
                threadCount = workers;
                Console.WriteLine($"Worker count set to {workers}");
            }

            var eventFlowConfigPath = settingsProvider.GetSetting("EventFlowConfigPath", "eventFlowConfig.json");
            using (var diagnosticsPipeline = DiagnosticPipelineFactory.CreatePipeline(eventFlowConfigPath))
            {
                var cts = new CancellationTokenSource();

                var tlsCertPath = settingsProvider.GetSetting("TlsCertificatePath", "protocol-gateway.contoso.com.pfx");
                var tlsCertPassword = settingsProvider.GetSetting("TlsCertificatePassword", "password");
                var certificate = new X509Certificate2(Path.Combine(AppContext.BaseDirectory, tlsCertPath), tlsCertPassword);
                BlobSessionStatePersistenceProvider blobSessionStateProvider = BlobSessionStatePersistenceProvider.CreateAsync(
                    settingsProvider.GetSetting("BlobSessionStatePersistenceProvider.StorageConnectionString"),
                    settingsProvider.GetSetting("BlobSessionStatePersistenceProvider.StorageContainerName")).Result;

                TableQos2StatePersistenceProvider tableQos2StateProvider = TableQos2StatePersistenceProvider.CreateAsync(
                    settingsProvider.GetSetting("TableQos2StatePersistenceProvider.StorageConnectionString"),
                    settingsProvider.GetSetting("TableQos2StatePersistenceProvider.StorageTableName")).Result;

                var bootstrapper = new Bootstrapper(settingsProvider, blobSessionStateProvider, tableQos2StateProvider);
                Task.Run(() => bootstrapper.RunAsync(certificate, threadCount, cts.Token), cts.Token);

                Console.ReadLine();
                cts.Cancel();
                bootstrapper.CloseCompletion.Wait(TimeSpan.FromSeconds(20));
            }
        }
    }

}