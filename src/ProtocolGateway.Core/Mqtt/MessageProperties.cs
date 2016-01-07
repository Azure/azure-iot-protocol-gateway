// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Mqtt
{
    public static class MessageProperties
    {
        public const string MessageType = "$Sys.MessageType";
        public const string UnmatchedFlagPropertyName = "$Sys.Unmatched";
        public const string SubjectPropertyName = "$Sys.Subject";
        public const string DeviceIdParam = "$Sys.deviceId";
    }
}