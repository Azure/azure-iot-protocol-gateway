// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.IotHub.Routing
{
    public interface IIotHubMessageRouter
    {
        /// <summary>
        /// Tries to route a device bound message and append route by message metadata
        /// </summary>
        /// <param name="routeType"></param>
        /// <param name="context"></param>
        /// <param name="topicName"></param>
        /// <returns></returns>
        bool TryRouteOutgoingMessage(RouteSourceType routeType, IMessage context, out string topicName);

        /// <summary>
        /// Tries to route the message to the destination and appends route properties to message metadata
        /// </summary>
        /// <param name="topicName"></param>
        /// <param name="context"></param>
        /// <param name="routeType"></param>
        /// <returns></returns>
        bool TryRouteIncomingMessage(string topicName, IMessage context, out RouteDestinationType routeType);
    }
}