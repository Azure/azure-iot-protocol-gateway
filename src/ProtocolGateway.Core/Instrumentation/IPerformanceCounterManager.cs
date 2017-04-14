// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Instrumentation
{
    public interface IPerformanceCounterManager
    {
        IPerformanceCounter GetCounter(string category, string name);
    }
}
