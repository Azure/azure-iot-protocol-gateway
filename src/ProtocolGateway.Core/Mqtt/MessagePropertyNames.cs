// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Mqtt
{
    public static class MessagePropertyNames
    {
        public const string ProtocolGatewayPrefix = "$PG.";
        public const string MessageType = ProtocolGatewayPrefix + "MessageType";
        public const string UnmatchedFlagPropertyName = "Unmatched";
        public const string SubjectPropertyName = "Subject";
        
    }
}