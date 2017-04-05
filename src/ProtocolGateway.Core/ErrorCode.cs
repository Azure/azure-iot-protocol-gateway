// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway
{
    public enum ErrorCode
    {
        InvalidErrorCode = 0,

        //BadRequest - 400
        InvalidPubAckOrder = 400001,
        GenericTimeOut = 400002,       
        InvalidOperation = 400003,
        NotSupported = 400004,
        KeepAliveTimedOut = 400005,
        ConnectionTimedOut = 400006,
        ConnectExpected = 400007,
        UnResolvedSendingClient = 400008,
        UnknownQosType = 400009,
        QoSLevelNotSupported = 400010,
        ExactlyOnceQosNotSupported = 400011,
        UnknownPacketType = 400012,
        DuplicateConnectReceived = 400013,

        AuthenticationFailed = 401000,

        //ClientClosedRequest - 499
        ChannelClosed = 499001,
    }
}
