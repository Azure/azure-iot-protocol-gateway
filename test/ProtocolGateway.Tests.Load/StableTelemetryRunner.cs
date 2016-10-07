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

    class StableTelemetryRunner : DeviceRunner
    {
        public StableTelemetryRunner(IEventLoopGroup eventLoopGroup, string deviceKey, string iotHubConnectionString, IPEndPoint endpoint, string tlsHostName)
            : base(eventLoopGroup, deviceKey, iotHubConnectionString, endpoint, tlsHostName)
        {
        }

        protected override string Name => "stable";

        protected override async Task GetScenario(IChannel channel, ReadListeningHandler readHandler, string clientId,
            CancellationToken cancellationToken)
        {
            await GetSubscribeSteps(channel, readHandler, clientId);

            while (!cancellationToken.IsCancellationRequested)
            {
                await GetPublishSteps(channel, readHandler, clientId, QualityOfService.AtLeastOnce, "devices/{0}/messages/events", 1, 138, 353);

                await channel.EventLoop.ScheduleAsync(() => { }, TimeSpan.FromMinutes(1), cancellationToken);
            }
        }
    }
}