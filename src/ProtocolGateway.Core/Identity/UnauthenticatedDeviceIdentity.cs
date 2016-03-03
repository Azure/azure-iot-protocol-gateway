// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Identity
{
    using System;

    public sealed class UnauthenticatedDeviceIdentity : IDeviceIdentity
    {
        public static IDeviceIdentity Instance { get; } = new UnauthenticatedDeviceIdentity();

        UnauthenticatedDeviceIdentity()
        {
        }

        public bool IsAuthenticated => false;

        public string Id
        {
            get { throw new InvalidOperationException("Accessing identity of device while authentication failed."); }
        }
    }
}