// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Instrumentation
{
    public class EmptyPerformanceCounterManager : IPerformanceCounterManager
    {
        public IPerformanceCounter GetCounter(string category, string name)
        {
            return new EmptyPerformanceCounter();
        }
    }
}
