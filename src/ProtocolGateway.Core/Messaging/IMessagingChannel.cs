// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Messaging
{
    using System;

    public interface IMessagingChannel
    {
        void Handle(IMessage message, IMessagingServiceClient sender);

        void Close(Exception cause);

        event EventHandler CapabilitiesChanged;
    }
}