// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Gateway.Samples.Console
{
    using System;
    using System.Diagnostics.Tracing;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Codecs.Mqtt;
    using DotNetty.Common.Concurrency;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using Gateway.Samples.Common;
    using Microsoft.Azure.Devices.Gateway.Cloud;
    using Microsoft.Azure.Devices.Gateway.Core;
    using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;

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

            var eventListener = new ObservableEventListener();
            eventListener.LogToConsole();
            eventListener.EnableEvents(BootstrapperEventSource.Log, EventLevel.Verbose);
            eventListener.EnableEvents(BridgeEventSource.Log, EventLevel.Verbose);
            eventListener.EnableEvents(ChannelEventSource.Log, EventLevel.Verbose);
            eventListener.EnableEvents(BootstrapEventSource.Log, EventLevel.Verbose);
            eventListener.EnableEvents(ExecutorEventSource.Log, EventLevel.Verbose);
            eventListener.EnableEvents(MqttEventSource.Log, EventLevel.Verbose);

            try
            {
                var cts = new CancellationTokenSource();

                var certificate = new X509Certificate2("gateway.contoso.com.pfx", "password");
                var settingsProvider = new DefaultSettingsProvider();
                BlobSessionStateManager sessionStateManager = BlobSessionStateManager.CreateAsync(
                    settingsProvider.GetSetting("SessionStateManager.StorageConnectionString"),
                    settingsProvider.GetSetting("SessionStateManager.StorageContainerName")).Result;

                var bootstrapper = new Bootstrapper(settingsProvider, sessionStateManager);
                Task.Run(() => bootstrapper.RunAsync(certificate, threadCount, cts.Token), cts.Token);

                while (true)
                {
                    string input = Console.ReadLine();
                    if (input != null && input.ToLowerInvariant() == "exit")
                    {
                        break;
                    }
                }

                cts.Cancel();
                bootstrapper.CloseCompletion.Wait(TimeSpan.FromSeconds(20));
            }
            finally
            {
                eventListener.Dispose();
            }
        }
    }
}