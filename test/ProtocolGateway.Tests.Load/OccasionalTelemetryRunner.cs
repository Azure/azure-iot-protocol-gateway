// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Tests.Load
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Codecs.Mqtt.Packets;
    using DotNetty.Transport.Channels;

    class OccasionalTelemetryRunner : DeviceRunner
    {
        public OccasionalTelemetryRunner(IEventLoopGroup eventLoopGroup, string deviceKey, string iotHubConnectionString, IPEndPoint endpoint, string tlsHostName)
            : base(eventLoopGroup, deviceKey, iotHubConnectionString, endpoint, tlsHostName)
        {
        }

        protected override string Name => "occasional";

        protected override async Task GetScenario(IChannel channel, ReadListeningHandler readHandler, string clientId,
            CancellationToken cancellationToken)
        {
            await GetSubscribeSteps(channel, readHandler, clientId);

            await GetPublishSteps(channel, readHandler, clientId, QualityOfService.AtLeastOnce, "devices/{0}/messages/events", 10, 138, 353);
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