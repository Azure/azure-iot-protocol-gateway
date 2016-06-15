// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Tests.Load
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    class RunnerHost
    {
        static readonly TimeSpan BatchRateInterval = TimeSpan.FromSeconds(0.2);

        readonly RunnerConfiguration[] runners;
        readonly IdProvider idProvider;
        readonly TimeSpan settlePeriod;

        public RunnerHost(IdProvider idProvider, IEnumerable<RunnerConfiguration> runners, TimeSpan settlePeriod)
        {
            this.runners = runners.ToArray();
            this.idProvider = idProvider;
            this.settlePeriod = settlePeriod;
        }

        public void Run(CancellationToken cancellationToken)
        {
            foreach (RunnerConfiguration runner in this.runners)
            {
                RunnerConfiguration runnerConfig = runner;
                Task.Run(
                    async () =>
                    {
                        int batchCount = (int)(runnerConfig.RampUpPeriod.Ticks / BatchRateInterval.Ticks);
                        for (int i = 1; i <= batchCount; i++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            int order = i;
                            Task.Run(
                                () => this.StartRunnerBatch(runnerConfig, order, batchCount, cancellationToken),
                                cancellationToken)
                                .LogOnFaulure();
                            await Task.Delay(BatchRateInterval, cancellationToken);
                        }
                    })
                    .LogOnFaulure();
            }
        }

        async Task StartRunnerBatch(RunnerConfiguration runner, int order, int batchCount,
            CancellationToken cancellationToken)
        {
            try
            {
                Console.WriteLine("Starting batch ({0}): {1} of {2}", runner.Name, order, batchCount);
                double pieceCount = (double)runner.Count / batchCount;
                int runnerCount = (int)(pieceCount * order) - (int)(pieceCount * (order - 1));
                IEnumerable<Task> runnerTasks =
                    from config in Enumerable.Repeat(runner, runnerCount)
                    select this.RunAsync(config, cancellationToken);
                await Task.WhenAll(runnerTasks);
            }
            catch (Exception ex)
            {
                Console.WriteLine("BATCH START FAILURE({0}/{1}): {2}", runner.Name, order, ex);
            }
        }

        async Task RunAsync(RunnerConfiguration rc, CancellationToken cancellationToken)
        {
            string id = this.idProvider.Get();
            Exception currentException;
            bool exceptionOnStart;
            do
            {
                currentException = null;
                exceptionOnStart = false;
                Task closeFuture = null;
                try
                {
                    closeFuture = await rc.StartFunc(id, cancellationToken);
                }
                catch (Exception ex)
                {
                    currentException = ex;
                    exceptionOnStart = true;
                }

                if (!exceptionOnStart)
                {
                    try
                    {
                        await closeFuture;
                    }
                    catch (Exception ex)
                    {
                        currentException = ex;
                    }
                }
            }
            while (!cancellationToken.IsCancellationRequested && await rc.ClosedFunc(id, currentException, exceptionOnStart));
        }
    }
}