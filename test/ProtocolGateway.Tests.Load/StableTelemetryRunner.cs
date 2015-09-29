// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Gateway.Tests.Load
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using DotNetty.Codecs.Mqtt.Packets;
    using DotNetty.Transport.Channels;
    using Microsoft.Azure.Devices.Gateway.Tests;

    class StableTelemetryRunner : DeviceRunner
    {
        public StableTelemetryRunner(IEventLoopGroup eventLoopGroup, string deviceKey, string iotHubConnectionString, IPEndPoint endpoint, string tlsHostName)
            : base(eventLoopGroup, deviceKey, iotHubConnectionString, endpoint, tlsHostName)
        {
        }

        protected override string Name
        {
            get { return "stable"; }
        }

        protected override IEnumerable<IEnumerable<TestScenarioStep>> GetScenarioNested(Func<object> currentMessageFunc, string clientId,
            CancellationToken cancellationToken)
        {
            yield return GetSubscribeSteps(currentMessageFunc, clientId);

            while (!cancellationToken.IsCancellationRequested)
            {
                yield return GetPublishSteps(currentMessageFunc, clientId, QualityOfService.AtLeastOnce, "devices/{0}/messages/events", 1, 138, 353);

                yield return new[] { TestScenarioStep.Wait(TimeSpan.FromMinutes(1)) };
            }
        }
    }
}