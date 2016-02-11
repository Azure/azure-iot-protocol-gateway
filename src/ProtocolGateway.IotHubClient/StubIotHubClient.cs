// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.IotHubClient
{
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Utilities;
    using Microsoft.Azure.Devices.ProtocolGateway.IotHub;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt;

    public class StubIotHubClient : IIotHubClient
    {
        public static readonly DeviceClientFactoryFunc Factory = deviceCredentials => Task.FromResult<IIotHubClient>(new StubIotHubClient());

        readonly CancellationTokenSource disposePromise;

        public StubIotHubClient()
        {
            this.disposePromise = new CancellationTokenSource();
        }

        public Task SendAsync(IMessage message)
        {
            return TaskEx.Completed;
        }

        public Task<IMessage> ReceiveAsync()
        {
            var tcs = new TaskCompletionSource<IMessage>();
            this.disposePromise.Token.Register(() => tcs.SetCanceled());
            return tcs.Task;
        }

        public Task AbandonAsync(string lockToken)
        {
            return TaskEx.Completed;
        }

        public Task CompleteAsync(string lockToken)
        {
            return TaskEx.Completed;
        }

        public Task RejectAsync(string lockToken)
        {
            return TaskEx.Completed;
        }

        public async Task DisposeAsync()
        {
            this.disposePromise.Cancel();
        }
    }
}