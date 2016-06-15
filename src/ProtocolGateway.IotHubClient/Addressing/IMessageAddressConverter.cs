// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.IotHubClient.Addressing
{
    using Microsoft.Azure.Devices.ProtocolGateway.Messaging;

    public interface IMessageAddressConverter
    {
        bool TryDeriveAddress(IMessage message, out string address);

        bool TryParseAddressIntoMessageProperties(string address, IMessage message);
    }
}