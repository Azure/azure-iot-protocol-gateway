// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Instrumentation
{
    public sealed class EmptyPerformanceCounter : IPerformanceCounter
    {
        public long RawValue { get; set; }
        public string CounterName { get; private set; }

        public EmptyPerformanceCounter(string name)
        {
            this.CounterName = name;
        }

        public EmptyPerformanceCounter()
        {
        }

        public float NextValue
        {
            get { return 0; }
        }

        public void Increment()
        {
        }

        public void IncrementBy(long value)
        {
        }

        public void Decrement()
        {
        }
    }
}