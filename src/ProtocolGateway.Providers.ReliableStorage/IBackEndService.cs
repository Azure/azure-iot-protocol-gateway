// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IBackEndService.cs" company="">
//   
// </copyright>
// <summary>
//   Defines the IBackEndService type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using Microsoft.ServiceFabric.Services.Remoting;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport;

[assembly: FabricTransportServiceRemotingProvider(RemotingListener = RemotingListener.V2Listener, RemotingClient = RemotingClient.V2Client)]
namespace Microsoft.Azure.Devices.ProtocolGateway.Providers.ReliableStorage
{
    using System.Threading.Tasks;

    using Microsoft.ServiceFabric.Services.Remoting;

    public interface IBackEndService : IService
    {
        /// <summary>
        /// The set session state async.
        /// </summary>
        /// <param name="identityId"> The identity id. </param>
        /// <param name="sessionState"> The session state. </param>
        /// <returns> The <see cref="Task"/>. </returns>
        Task SetSessionStateAsync(string identityId, ReliableSessionState sessionState);

        /// <summary>
        /// The delete session state async.
        /// </summary>
        /// <param name="identityId"> The identity id. </param>
        /// <returns> The <see cref="Task"/>. </returns>
        Task DeleteSessionStateAsync(string identityId);

        /// <summary>
        /// The get session state async.
        /// </summary>
        /// <param name="identityId"> The identity id. </param>
        /// <returns> The <see cref="Task"/>. </returns>
        Task<ReliableSessionState> GetSessionStateAsync(string identityId);

        /// <summary>
        /// The get message async.
        /// </summary>
        /// <param name="identityId">
        /// The identity id.
        /// </param>
        /// <param name="packetId">
        /// The packet id.
        /// </param>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        Task<ReliableMessageDeliveryState> GetMessageAsync(string identityId, int packetId);

        /// <summary>
        /// The delete message async.
        /// </summary>
        /// <param name="identityId">
        /// The identity id.
        /// </param>
        /// <param name="packetId">
        /// The packet id.
        /// </param>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        Task DeleteMessageAsync(string identityId, int packetId);

        /// <summary>
        /// The set message async.
        /// </summary>
        /// <param name="identityId">
        /// The identity id.
        /// </param>
        /// <param name="packetId">
        /// The packet id.
        /// </param>
        /// <param name="message">
        /// The message.
        /// </param>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        Task SetMessageAsync(string identityId, int packetId, ReliableMessageDeliveryState message);
    }
}