// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Gateway.Tests.Load
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Codecs.Mqtt.Packets;
    using DotNetty.Transport.Channels;
    using Microsoft.Azure.Devices.Gateway.Tests;

    class OccasionalTelemetryRunner : DeviceRunner
    {
        public OccasionalTelemetryRunner(IEventLoopGroup eventLoopGroup, string deviceKey, string iotHubConnectionString, IPEndPoint endpoint, string tlsHostName)
            : base(eventLoopGroup, deviceKey, iotHubConnectionString, endpoint, tlsHostName)
        {
        }

        protected override string Name
        {
            get { return "occasional"; }
        }

        protected override IEnumerable<IEnumerable<TestScenarioStep>> GetScenarioNested(Func<object> currentMessageFunc, string clientId,
            CancellationToken cancellationToken)
        {
            yield return GetSubscribeSteps(currentMessageFunc, clientId);

            yield return GetPublishSteps(currentMessageFunc, clientId, QualityOfService.AtLeastOnce, "devices/{0}/messages/events", 10, 138, 353);
        }

        public override async Task<bool> OnClosedAsync(string deviceId, Exception exception, bool onStart)
        {
            if (exception != null)
            {
                return await base.OnClosedAsync(deviceId, exception, onStart);
            }

            await Task.Delay(TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(Random.Next(-5, 6))); // 10 min +/- 5 sec
            return true;
        }
    }
}