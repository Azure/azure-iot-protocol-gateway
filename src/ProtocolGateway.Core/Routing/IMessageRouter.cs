// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Routing
{
    using Microsoft.Azure.Devices.ProtocolGateway.Messaging;

    public interface IMessageRouter
    {
        /// <summary>
        /// Tries to route a device bound message and append route by message metadata
        /// </summary>
        /// <param name="routeType"></param>
        /// <param name="context"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        bool TryRouteOutgoingMessage(RouteSourceType routeType, IMessage context, out string path);

        /// <summary>
        /// Tries to route the message to the destination and appends route properties to message metadata
        /// </summary>
        /// <param name="path"></param>
        /// <param name="context"></param>
        /// <param name="routeType"></param>
        /// <returns></returns>
        bool TryRouteIncomingMessage(string path, IMessage context, out RouteDestinationType routeType);
    }
}