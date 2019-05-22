// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.IotHubClient
{
    using System;
    using Microsoft.Azure.Devices.Client;

    public class DeviceClientRetryPolicy : IRetryPolicy
    {
        public static DeviceClientRetryPolicy Instance { get; } = new DeviceClientRetryPolicy();

        DeviceClientRetryPolicy()
        { }

        public bool ShouldRetry(int currentRetryCount, Exception lastException, out TimeSpan retryInterval)
        {
            // No retry for now. We will tune it as we progress.
            retryInterval = TimeSpan.Zero;
            return false;
        }
    }
}
