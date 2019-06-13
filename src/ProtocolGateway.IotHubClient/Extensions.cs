// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.IotHubClient
{
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.ProtocolGateway.Messaging;

    static class Extensions
    {
        public static MessagingException ToMessagingException(this IotHubException ex)
        {
            return new MessagingException("Error communicating with IoT Hub. Tracking id: " + ex.TrackingId, ex, ex.IsTransient, ex.TrackingId);
        }
    }
}
