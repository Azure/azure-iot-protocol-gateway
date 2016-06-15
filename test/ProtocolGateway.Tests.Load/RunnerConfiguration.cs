// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Tests.Load
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public class RunnerConfiguration
    {
        public RunnerConfiguration(string name, Func<string, CancellationToken, Task<Task>> startFunc,
            Func<string, Exception, bool, Task<bool>> closedFunc, int count, TimeSpan rampUpPeriod)
        {
            this.StartFunc = startFunc;
            this.ClosedFunc = closedFunc;
            this.Count = count;
            this.RampUpPeriod = rampUpPeriod;
            this.Name = name;
        }

        public string Name { get; private set; }

        public int Count { get; private set; }

        public TimeSpan RampUpPeriod { get; private set; }

        /// <summary>
        ///     in: id, out: future for start completion wrapping future for subsequent closure
        /// </summary>
        public Func<string, CancellationToken, Task<Task>> StartFunc { get; private set; }

        /// <summary>
        ///     in: Exception on closure (if any), out: future task for decision whether to restart the runner; future can be used
        ///     to delay execution
        /// </summary>
        public Func<string, Exception, bool, Task<bool>> ClosedFunc { get; private set; }
    }
}