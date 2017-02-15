// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway
{
    using System;

    public class ProtocolGatewayException : Exception
    {
        public bool IsTransient { get; }

        public string TrackingId { get; private set; }

        public ErrorCode ErrorCode { get; private set; }

        public ProtocolGatewayException(ErrorCode errorCode, string message)
            : this(errorCode, message, false)
        {
        }

        public ProtocolGatewayException(ErrorCode errorCode, string message, string trackingId)
            : this(errorCode, message, false, trackingId)
        {
        }

        public ProtocolGatewayException(ErrorCode errorCode, string message, bool isTransient)
            : this(errorCode, message, isTransient, string.Empty)
        {
        }

        public ProtocolGatewayException(ErrorCode errorCode, string message, bool isTransient, string trackingId)
            : base(message)
        {
            this.IsTransient = isTransient;
            this.TrackingId = trackingId;
            this.ErrorCode = errorCode;
        }
    }
}
