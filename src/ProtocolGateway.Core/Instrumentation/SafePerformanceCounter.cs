// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Instrumentation
{
    using System;
    using System.Diagnostics;

    public sealed class SafePerformanceCounter : IPerformanceCounter
    {
        readonly PerformanceCounter counter;
        
        public SafePerformanceCounter(PerformanceCounter counter)
        {
            this.counter = counter;
        }

        public long RawValue
        {
            get { return this.counter.RawValue; }
            set { this.counter.RawValue = value; }
        }

        public float NextValue
        {
            get
            {
                return this.counter.NextValue();
            }
        }

        public void Increment()
        {
            try
            {
                this.counter.Increment();
            }
            catch (Exception ex)
            {
                CommonEventSource.Log.Verbose("Failed to increment perf counter: " + ex.ToString());
            }
        }

        public void IncrementBy(long value)
        {
            try
            {
                this.counter.IncrementBy(value);
            }
            catch (Exception ex)
            {
                CommonEventSource.Log.Verbose("Failed to increment perf counter: " + ex.ToString());
            }
        }

        public void Decrement()
        {
            try
            {
                this.counter.Decrement();
            }
            catch (Exception ex)
            {
                CommonEventSource.Log.Verbose("Failed to decrement perf counter: " + ex.ToString());
            }
        }
    }
}