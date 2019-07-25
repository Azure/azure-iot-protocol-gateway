#### 3.0.0 July 24, 2019
- Change the way `MqttAdapter` handles transient sessions. `MqttAdapter.ProcessPendingSubscriptionChanges` will now always call `ISessionStatePersistenceProvider.SetAsync` irrespective of the session being transient or not. State handling of transient sessions is now left to the implementation of '`ISessionStatePersistenceProvider`
- `BlobSessionStatePersistenceProvider.SetAsync` will return instead of throwing if the state is transient 

#### 1.0.1 March 12
- Azure Table QoS 2 persistence provider uses proper query API to retrieve only relevant row.
- QoS 2 persistence provider API is modified to work with SequenceNumber instead of MessageId.
- Azure Blob session state persistence provider is modified to update ETag value on in-memory session state object to allow saving it later on without any issue.
- 14 least significant bits are used when converting SequenceNumber into PacketId.
- `IMessage.SequenceNumber` type is changed to ulong.

#### 1.0.0 March 03
- `IQos2StatePersistenceProvider` now accepts device identity to scope messages in flight to device level.
- `IAuthenticationProvider` has been renamed into `IDeviceIdentityProvider` and the API has changed to allow for a flexible interaction between device identity source and other protocol gateway components.
- `ITopicNameRouter` has been renamed into `IMessageRouter` and the API generalized to work with the message as a whole.
- `IDeviceClient` has been renamed into `IMessagingServiceClient` to better reflect the purpose of the component. `IMessagingFactory` was introduced and message exchange with `IMessagingServiceClient` is done using `IMessage` objects.
- Issues #23, #26, #27, #28, #33 have been addressed.
- Microsoft Azure IoT SDK specific components have been moved to `ProtocolGateway.IotHubClient` project.
- Components are now released as 3 nuget packages according to certain dependencies (Azure Storage, Azure IoT SDK):
 - `Microsoft.Azure.Devices.ProtocolGateway.Core`,
 - `Microsoft.Azure.Devices.ProtocolGateway.IotHubClient`,
 - `Microsoft.Azure.Devices.ProtocolGateway.Providers.CloudStorage`.
- Renamed `samples` into `host` throughout the solution.
