// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence
{
    using System.Threading.Tasks;

    public interface IQos2StatePersistenceProvider
    {
        IQos2MessageDeliveryState Create(string messageId);

        /// <summary>
        ///     Performs a lookup of message delivery state by packet identifier
        /// </summary>
        /// <param name="packetId">Packet identifier.</param>
        /// <returns>
        ///     <see cref="IQos2MessageDeliveryState" /> object if message was previously persisted with
        ///     the given packet id; null - if no message could be found.
        /// </returns>
        Task<IQos2MessageDeliveryState> GetMessageAsync(int packetId);

        Task DeleteMessageAsync(int packetId, IQos2MessageDeliveryState message);

        Task SetMessageAsync(int packetId, IQos2MessageDeliveryState message);
    }
}