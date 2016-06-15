// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Instrumentation
{
    using DotNetty.Common;

    public sealed class AveragePerformanceCounter
    {
        readonly SafePerformanceCounter countCounter;
        readonly SafePerformanceCounter baseCounter;

        public AveragePerformanceCounter(SafePerformanceCounter countCounter, SafePerformanceCounter baseCounter)
        {
            this.countCounter = countCounter;
            this.baseCounter = baseCounter;
        }

        public void Register(PreciseTimeSpan startTimestamp)
        {
            PreciseTimeSpan elapsed = PreciseTimeSpan.FromStart - startTimestamp;
            long elapsedMs = (long)elapsed.ToTimeSpan().TotalMilliseconds;
            this.countCounter.IncrementBy(elapsedMs);
            this.baseCounter.Increment();
        }
    }
}