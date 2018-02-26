// <copyright file="ReliableSessionStatePersistenceProvider.cs" company="Microsoft">
//   ReliableSessionStatePersistenceProvider
// </copyright>
// <summary>
//   Defines the ReliableSessionStatePersistenceProvider type.
// </summary>

namespace Microsoft.Azure.Devices.ProtocolGateway.Providers.ReliableStorage
{
    using System;
    using System.Diagnostics.Tracing;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.ProtocolGateway.Identity;
    using Microsoft.Azure.Devices.ProtocolGateway.Instrumentation;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence;
    using Microsoft.ServiceFabric.Services.Client;
    using Microsoft.ServiceFabric.Services.Remoting.Client;

    using Murmur;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// The reliable session state persistence provider.
    /// </summary>
    public class ReliableSessionStatePersistenceProvider : ISessionStatePersistenceProvider
    {
        readonly int partitionCount;
        static readonly Uri BackEndEndpoint = new Uri("fabric:/ProtocolGateway.Host.Fabric/BackEnd");

        /// <summary>
        /// The serializer settings.
        /// </summary>
        static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            Converters =
            {
                new SubscriptionConverter()
            }
        };

        /// <summary>
        /// The partition keys.
        /// </summary>
        readonly string[] partitionKeys;

        public ReliableSessionStatePersistenceProvider(int partitionCount)
        {
            this.partitionCount = partitionCount;
            this.partitionKeys = Enumerable.Range(0, this.partitionCount).Select(i => $"BackEnd_{i}").ToArray(); // WARNING: Do not change naming convention
        }

        /// <summary>
        /// The create.
        /// </summary>
        /// <param name="transient">
        /// The transient.
        /// </param>
        /// <returns>
        /// The <see cref="ISessionState"/>.
        /// </returns>
        public ISessionState Create(bool transient)
        {
            return new ReliableSessionState(transient);
        }

        /// <summary>
        /// The get async.
        /// </summary>
        /// <param name="identity">
        /// The identity.
        /// </param>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        public async Task<ISessionState> GetAsync(IDeviceIdentity identity)
        {
            var partitionKey = this.CalculatePartitionKey(identity.Id);
            if (CommonEventSource.Log.IsVerboseEnabled)
            {
                CommonEventSource.Log.Verbose($"Selecting partition {partitionKey} of SF reliable state for getting Device {identity.Id} MQTT session state", string.Empty);
            }
            var servicePartitionKey = new ServicePartitionKey(partitionKey);
            var backEndService = ServiceProxy.Create<IBackEndService>(BackEndEndpoint, servicePartitionKey);
            return await backEndService.GetSessionStateAsync(identity.Id).ConfigureAwait(false);
        }

        /// <summary>
        /// The set async.
        /// </summary>
        /// <param name="identity">
        /// The identity.
        /// </param>
        /// <param name="sessionState">
        /// The session state.
        /// </param>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// </exception>
        public async Task SetAsync(IDeviceIdentity identity, ISessionState sessionState)
        {
            var state = sessionState as ReliableSessionState;
            if (state == null)
            {
                throw new ArgumentException("Cannot set Session State object that hasn't been acquired from provider.", nameof(sessionState));
            }

            if (state.IsTransient)
            {
                throw new ArgumentException("Cannot persist transient Session State object.", nameof(sessionState));
            }

            var partitionKey = this.CalculatePartitionKey(identity.Id);
            if (CommonEventSource.Log.IsVerboseEnabled)
            {
                CommonEventSource.Log.Verbose($"Selecting partition {partitionKey} of SF reliable state for getting Device {identity.Id} MQTT session state", string.Empty);
            }
            var servicePartitionKey = new ServicePartitionKey(partitionKey);
            var backEndService = ServiceProxy.Create<IBackEndService>(BackEndEndpoint, servicePartitionKey);
            await backEndService.SetSessionStateAsync(identity.Id, state).ConfigureAwait(false);
        }

        /// <summary>
        /// The delete async.
        /// </summary>
        /// <param name="identity">
        /// The identity.
        /// </param>
        /// <param name="sessionState">
        /// The session state.
        /// </param>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// </exception>
        public async Task DeleteAsync(IDeviceIdentity identity, ISessionState sessionState)
        {
            var state = sessionState as ReliableSessionState;
            if (state == null)
            {
                throw new ArgumentException("Cannot set Session State object that hasn't been acquired from provider.", nameof(sessionState));
            }

            var partitionKey = this.CalculatePartitionKey(identity.Id);
            if (CommonEventSource.Log.IsVerboseEnabled)
            {
                CommonEventSource.Log.Verbose($"Selecting partition {partitionKey} of SF reliable state for getting Device {identity.Id} MQTT session state", string.Empty);
            }
            var servicePartitionKey = new ServicePartitionKey(partitionKey);
            var backEndService = ServiceProxy.Create<IBackEndService>(BackEndEndpoint, servicePartitionKey);
            await backEndService.DeleteSessionStateAsync(identity.Id).ConfigureAwait(false);
        }

        /// <summary>
        /// The calculate partition key.
        /// </summary>
        /// <param name="identifier">
        /// The identifier.
        /// </param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        string CalculatePartitionKey(string identifier)
        {
            byte[] hash = MurmurHash.Create32(0, false).ComputeHash(Encoding.ASCII.GetBytes(identifier));
            return this.partitionKeys[BitConverter.ToUInt32(hash, 0) % this.partitionCount];
        }
    }

    /// <summary>
    /// The subscription converter.
    /// </summary>
    public class SubscriptionConverter : CustomCreationConverter<ISubscription>
    {
        /// <summary>
        /// The create.
        /// </summary>
        /// <param name="objectType">
        /// The object type.
        /// </param>
        /// <returns>
        /// The <see cref="ISubscription"/>.
        /// </returns>
        public override ISubscription Create(Type objectType)
        {
            return new Subscription();
        }
    }
}
