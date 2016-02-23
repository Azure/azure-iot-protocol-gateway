// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.IotHub
{
    using System.Threading.Tasks;

    public interface IIotHubClient
    {
        Task SendAsync(IMessage message);

        Task<IMessage> ReceiveAsync();

        Task AbandonAsync(string lockToken);

        Task CompleteAsync(string lockToken);

        Task RejectAsync(string lockToken);

        Task DisposeAsync();
    }
}