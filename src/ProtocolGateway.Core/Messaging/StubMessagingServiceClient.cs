// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Messaging
{
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Utilities;

    public class StubMessagingServiceClient : IMessagingServiceClient
    {
        readonly CancellationTokenSource disposePromise;

        public StubMessagingServiceClient()
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
            this.disposePromise.Token.Register(state => ((TaskCompletionSource<IMessage>)state).TrySetCanceled(), tcs);
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

        public Task DisposeAsync()
        {
            this.disposePromise.Cancel();
            return TaskEx.Completed;
        }
    }
}