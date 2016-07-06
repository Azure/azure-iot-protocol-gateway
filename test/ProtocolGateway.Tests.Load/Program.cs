// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Tests.Load
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using CommandLine;
    using DotNetty.Transport.Channels;

    class Program
    {
        static void Main(string[] args)
        {
            var options = new Options();
            if (!Parser.Default.ParseArgumentsStrict(args, options))
            {
                //Console.WriteLine(HelpText.AutoBuild(options).ToString());
                return;
            }

            if (options.CreateDeviceCount > 0)
            {
                CreateDevices(options).Wait();
            }

            string[] runnerConfigurationOptions = options.Runners;
            if (runnerConfigurationOptions.Length % 3 != 0)
            {
                Console.WriteLine("Could not parse runner configuration. Make sure runners are described in threes.");
            }

            var group = new MultithreadEventLoopGroup(Environment.ProcessorCount);
            var idProvider = new IdProvider(options.DeviceStartingFrom, options.DeviceNamePattern);

            var runnerConfigurations = new List<RunnerConfiguration>();
            foreach (List<string> runnerOptions in runnerConfigurationOptions.InSetsOf(3))
            {
                string name = runnerOptions[0];
                double startFrequency = double.Parse(runnerOptions[1]);
                int count = int.Parse(runnerOptions[2]);
                RunnerConfiguration config;
                switch (name)
                {
                    case "stable":
                        var stableRunner = new StableTelemetryRunner(group, options.DeviceKey, options.IotHubConnectionString,
                            new IPEndPoint(IPAddress.Parse(options.Address), options.Port), options.HostName);
                        config = stableRunner.CreateConfiguration(count, TimeSpan.FromSeconds(count / startFrequency));
                        break;
                    case "occasional":
                        var occasionalRunner = new OccasionalTelemetryRunner(group, options.DeviceKey, options.IotHubConnectionString,
                            new IPEndPoint(IPAddress.Parse(options.Address), options.Port), options.HostName);
                        config = occasionalRunner.CreateConfiguration(count, TimeSpan.FromSeconds(count / startFrequency));
                        break;
                    default:
                        Console.WriteLine("Unknown runner configuration `{0}`", name);
                        return;
                }
                runnerConfigurations.Add(config);
            }

            var cts = new CancellationTokenSource();
            var host = new RunnerHost(idProvider, runnerConfigurations, TimeSpan.FromSeconds(10));
            host.Run(cts.Token);

            Console.ReadLine();
            Console.WriteLine("Closing runners");
            cts.Cancel();
        }

        static async Task CreateDevices(Options options)
        {
            if (string.IsNullOrEmpty(options.DeviceKey))
            {
                throw new ArgumentException("Device key was not specified.");
            }

            RegistryManager registryManager = RegistryManager.CreateFromConnectionString(options.IotHubConnectionString);
            await registryManager.OpenAsync();
            const int BatchSize = 500;
            var tasks = new List<Task>(BatchSize);
            foreach (List<int> pack in Enumerable.Range(options.DeviceStartingFrom, options.CreateDeviceCount).InSetsOf(BatchSize))
            {
                tasks.Clear();
                Console.WriteLine("Creating devices {0}..{1}", pack.First(), pack.Last());
                foreach (int i in pack)
                {
                    string deviceId = string.Format(options.DeviceNamePattern, i);
                    var device = new Device(deviceId)
                    {
                        Authentication = new AuthenticationMechanism
                        {
                            SymmetricKey = new SymmetricKey
                            {
                                PrimaryKey = options.DeviceKey,
                                SecondaryKey = options.DeviceKey2
                            }
                        }
                    };
                    tasks.Add(registryManager.AddDeviceAsync(device));
                }
                await Task.WhenAll(tasks);
            }
        }
    }
}