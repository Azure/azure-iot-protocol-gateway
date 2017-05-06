// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ProtocolGateway.Host.Console.NetStandard
{
    using System;
    using System.Diagnostics.Tracing;
    using System.IO;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Internal.Logging;
    using Microsoft.Azure.Devices.ProtocolGateway;
    using Microsoft.Azure.Devices.ProtocolGateway.Instrumentation;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence;
    using ProtocolGateway.Host.Common;

    class Program
    {
        static void Main(string[] args)
        {
            int threadCount = Environment.ProcessorCount;
            if (args.Length > 0)
            {
                threadCount = int.Parse(args[0]);
            }

            var eventListener = new ConsoleEventListener();

            eventListener.EnableEvents(BootstrapperEventSource.Log, EventLevel.Verbose);
            eventListener.EnableEvents(CommonEventSource.Log, EventLevel.Verbose);
            eventListener.EnableEvents(DefaultEventSource.Log, EventLevel.Verbose);

            try
            {
                var cts = new CancellationTokenSource();

                var certificate = new X509Certificate2(Path.Combine(AppContext.BaseDirectory, "protocol-gateway.contoso.com.pfx"), "password");
                var settingsProvider = new AppConfigSettingsProvider();
                ISessionStatePersistenceProvider sessionStateProvider = new TransientSessionStatePersistenceProvider();

                var bootstrapper = new Bootstrapper(settingsProvider, sessionStateProvider, null);
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