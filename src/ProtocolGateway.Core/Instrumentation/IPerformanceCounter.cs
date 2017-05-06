// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Instrumentation
{
    public interface IPerformanceCounter
    {
        long RawValue { get; set; }
        float NextValue { get;  }
        void Increment();
        void IncrementBy(long value);
        void Decrement();
    }
}