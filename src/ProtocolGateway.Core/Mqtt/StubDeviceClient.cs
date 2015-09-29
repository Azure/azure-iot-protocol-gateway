// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Mqtt
{
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Utilities;
    using Microsoft.Azure.Devices.Client;

    public class StubDeviceClient : IDeviceClient
    {
        public static readonly DeviceClientFactoryFunc Factory = deviceCredentials => Task.FromResult<IDeviceClient>(new StubDeviceClient());

        readonly CancellationTokenSource disposePromise;

        public StubDeviceClient()
        {
            this.disposePromise = new CancellationTokenSource();
        }

        public Task SendAsync(Message message)
        {
            return TaskEx.Completed;
        }

        public Task<Message> ReceiveAsync()
        {
            var tcs = new TaskCompletionSource<Message>();
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