# Microsoft Azure IoT Protocol Gateway Developer Guide

Azure IoT protocol gateway enables devices to communicate with Microsoft Azure IoT Hub over MQTT and potentially other protocols. The protocol gateway provides a model for creating protocol adapters for different protocols. These adapters can be plugged into the execution environment through a ‘pipeline’ concept. The execution pipeline can also contain components (i.e. handlers) performing specialized processing of the data before it is passed to IoT Hub. 
The MQTT protocol adapter enables connectivity with IoT devices (or other clients) over the MQTT v3.1.1 protocol. The gateway bridges the communication to Azure IoT Hub using the standard IoT Hub client over the AMQP 1.0 protocol.

The protocol gateway is built with customization in mind. In order of complexity, you can
- change the configuration options
- change the initialization parameters
- facilitate extension points
- modify components of the gateway.

This guide describes the protocol gateway behavior, component structure, extension points and intended ways of customization. For deployment and setup instructions please see [Azure IoT Protocol Gateway - Getting Started Guide](../README.md).

The terms ‘device’ or ‘devices’ are used in this guide as a generic way to indicate IoT devices or field gateways, but could also include other types of clients establishing communication with IoT Hub. ‘Protocol gateway’ or ‘gateway’ are used to refer to the Azure IoT protocol gateway.

## MQTT Protocol Adapter 
The MQTT Protocol Adapter is implemented in the class MqttIotHubAdapter. The adapter is the channel handler that bridges the communication with IoT Hub. This component is responsible for the translation of the MQTT protocol to IoT Hub expected syntax and semantics. The implementation is based on the MQTT 3.1.1 protocol version. The following topics describe implementation specific details related to the MQTT protocol features.

### Authentication
The default implementation relies on authentication through IoT Hub. The user name and password fields from the MQTT CONNECT packet are passed through to IoT Hub for authentication. The expected values for those fields are the device identifier (deviceId) in the User Name and the device Shared-Access-Signature (SAS) token in the Password field of the MQTT packet. This behavior can be changed by providing a custom implementation of the IDeviceIdentityProvider interface.

### Connecting to the Gateway
As specified by the MQTT standard, only one connection can be established from each device. The detection of available connections from a particular device is implemented in IoT Hub. Note that because the protocol gateway will typically run on multiple nodes in a scale-out configuration, a device might have an open connection with IoT Hub established through a different protocol gateway node. Since IoT Hub supports multiple links per device, this detection is specifically performed when the protocol gateway opens a receive link to IoT Hub. In case IoT Hub detects an existing receiving link for the device, it will close the existing link and then accept the new link. This also means that when a receiving link is closed by IoT Hub, the protocol gateway closes the sending link to IoT Hub and then closes the MQTT network connection.

If a device is deactivated in IoT Hub, then the protocol gateway will not accept connect requests from the device by responding with CONNACK with error code 0x03. If the device becomes deactivated during an active MQTT session, then the gateway will complete any outstanding message exchange (i.e. messages pending transmission, or packet identifiers in-flight) and then close the network connection.

### Subscriptions and Topic Names
IoT Hub has an endpoint for sending messages to the cloud and an endpoint for receiving messages. Devices can publish messages with the topic name ‘devices/{deviceId}/messages/events’ in order to send messages to IoT Hub. They can subscribe to the topic name ‘devices/{deviceId}/messages/devicebound’ to receive messages. The MQTT ClientId is used as device identifier in the variable `{deviceId}`. 
The protocol adapter uses config parameters with a URI template for each IoT Hub endpoint, which enables translation of incoming and outgoing topic names for MQTT messages. The parameter `<inboundRoute to="telemetry">` specifies the template for telemetry messages to the cloud, while the parameter `<outboundRoute from="notification">` is used for the receiving endpoint. In addition to the `{deviceId}` variable, these URI templates can contain other variables as well. Using the URI template config allows devices to use a different structure for the topic names than the IoT Hub namespace. Commonly the main part of a topic name will follow the namespace of the IoT Hub endpoint, but multiple topic levels can be added to it to provide additional information. This ‘suffix’ will usually be translated into message properties, while the main topic path will address the IoT Hub endpoint.

When a device publishes a message to the gateway, the topic name will be matched against the `<inboundRoute to="telemetry">` URI template. If the template contains variables, each variable that matches a topic level will be added to the message as a property with the appropriate value from the topic name. 
If the topic name of a message doesn’t match the corresponding template, two special properties will be added to the message: ‘Unmatched’ set to ‘True’ and 'Subject' set to the topic name from the MQTT message. This allows the backend application to recognize that the topic name couldn’t be translated into message properties as defined by the template. 

Similarly, when receiving messages from IoT Hub, the topic for the MQTT message will be generated based on the URI template `<outboundRoute from="notification">`, and parts of it can be populated with message properties from the header of the message.
The generated topic will then be matched against the list of client subscriptions and published to the device as appropriate. If there is no matching client subscription the message will be rejected. If the backend app has requested feedback on this message, IoT Hub will enqueue a message in the feedback queue with a "rejected" outcome for it. 

### Quality of Service (QoS)
Azure IoT Hub provides at least once delivery semantics. The protocol adapter implements QoS 0 and QoS 1 for sending and receiving messages. In addition, it implements QoS 2 for publishing packets from the protocol gateway to the device (i.e. Server to Client). The state of messages in-flight is tracked through a persistent state provider. The default implementation of the state provider uses Azure storage. The state provider is a well-isolated component and can be replaced by a custom implementation if desired.

### Retain Messages
When a device publishes a packet with the retain flag, the protocol adapter adds a message property with a configurable name and value of “True” to the message before forwarding it to IoT Hub. The name of the property is defined by the RetainProperty config parameter. This property is used to indicate a retain message to the backend solution, which can process it appropriately.

### Will Messages
Will messages are forwarded as telemetry messages to the IoT Hub telemetry endpoint (as specified by the `<inboundRoute to="telemetry">` config parameter) when the network is disconnected and the will message has not been removed from the session state through a DISCONNECT packet.

### Message Ordering
As defined by the MQTT 3.1.1 protocol, the gateway provides an in order message delivery. The protocol gateway uses five separate queues to keep track of messages in-flight. Three of the queues are used for tracking outstanding responses for cloud-to-device messages, i.e. for PUBACK, PUBREC, and PUBCOMP packets. Two queues are used for tracking device-to-cloud messages, specifically for PUBLISH (and PUBACK) and SUBSCRIBE/UNSUBSCRIBE (and SUBACK/UNSUBACK respectively) traffic. These queues ensure that ordering is handled for each group separately. The gateway publishes messages to the device as they are available or as they arrive in the IoT Hub device queue. Thus, multiple messages can be in-flight at the same time. If an ACK is not received for a specified period of time, the gateway switches to a retransmission mode and will start resending unacknowledged packet identifiers.

## Extensibility 

### Authentication Provider
When a device connects to the protocol gateway, it provides credentials through the client ID, user name, and password fields in the CONNECT packet.
The gateway defines an Authentication Provider component in order to validate device credentials and also to return credentials for connecting to IoT Hub on behalf of the device.
The default implementation of the gateway uses `SasTokenAuthenticationProvider` which:
- verifies that the user name is equal to client ID
- uses the client ID as the device ID
- assumes that the password contains a well-formed IoT Hub SAS Token

If you require custom authentication (for example, to lookup the device in a custom registry), you need to:

1. Implement the [`IDeviceIdentityProvider` interface](../src/ProtocolGateway.Core/Identity/IDeviceIdentityProvider.cs).
See [SasTokenAuthenticationProvider](../src/ProtocolGateway.Core/Mqtt/Auth/SasTokenAuthenticationProvider.cs) for a reference implementation.
2. Change `Bootstrapper`'s constructor to use your custom Authentication Provider implementation:
```
this.authProvider = new MyCustomAuthenticationProvider(...);
```

### Session State Persistence Provider
When a device connects to the gateway with `CleanSession = 0` in the CONNECT packet, the gateway will persist the session state between connections through the Session State Persistence Provider component.
In the default implementation, the protocol gateway provides a `BlobSessionStatePersistenceProvider` that uses Microsoft Azure Storage blobs to store session state data.

You can easily replace the default provider with a custom one, by following these steps:

1. Implement the [`ISessionStatePersistenceProvider` interface](../src/ProtocolGateway.Core/Mqtt/Persistence/ISessionStatePersistenceProvider.cs).
See [BlobSessionStatePersistenceProvider](../src/ProtocolGateway.Providers.CloudStorage/BlobSessionStatePersistenceProvider.cs) for a reference implementation. 
2. Change the entry point (e.g. `WorkerRole`) to instantiate your provider in the `Bootstrapper` initialization:
```
var myProvider = new MyCustomSessionStatePersistenceProvider(...);
...
var bootstrapper = new Bootstrapper(
    settingsProvider,
    myProvider,
    tableQos2StateProvider);
```

### QoS 2 State Persistence Provider 

Protocol gateway supports QoS 2 for cloud-to-device communication (from IoT Hub to devices). In order to ensure "Exactly Once" delivery guarantee for persisted MQTT sessions (`CleanSession = 0`), the gateway defines a QoS 2 State Persistence Provider.
By default, the gateway utilizes `TableQos2StatePersistenceProvider`, which uses Microsoft Azure Storage Tables to store delivery state for QoS 2 messages in-flight.

You can easily replace the default provider with a custom one by following these steps:

1. Implement the [`IQos2StatePersistenceProvider` interface](../src/ProtocolGateway.Core/Mqtt/Persistence/IQos2StatePersistenceProvider.cs).
See [TableQos2StatePersistenceProvider](../src/ProtocolGateway.Providers.CloudStorage/TableQos2StatePersistenceProvider.cs) for a reference implementation. 
2. Change the entry point (e.g. `WorkerRole`) to instantiate your provider in the `Bootstrapper` initialization:
```
var myProvider = new MyCustomQos2StatePersistenceProvider(...);
...
var bootstrapper = new Bootstrapper(
    settingsProvider,
    blobSessionStateProvider,
    myProvider);
```

### Custom Channel Handlers
Protocol gateway uses pipeline-based message processing using the following handlers: TLS encryption/decryption, MQTT codec (encoder and decoder), and MqttIotHubAdapter. One way for customizing the gateway behavior is by injecting additional handlers in this pipeline. By adding a custom handler between the MQTT codec and the MqttIotHubAdapter you can inspect, modify, discard, delay, or split the passing messages.

Here is an example of a handler that will discard any incoming PUBLISH packet if its topic name ends with "verbose":
```
class DiscardVerbosePublishPacketHandler : ChannelHandlerAdapter
{
    public override void ChannelRead(IChannelHandlerContext context, object message)
    {
        var publishPacket = message as PublishPacket;
        if (message == null || !publishPacket.TopicName.EndsWith("verbose", StringComparison.OrdinalIgnoreCase))
        {
            // passing message through
            context.FireChannelRead(message);                        
        }
        else
        {
            // discarding message - release its payload's Byte Buffer
            publishPacket.Release();
        }
    }    
}
```

The handler lets through all messages except for PUBLISH packets that have a topic name ending with "verbose". When such a PUBLISH packet gets to the handler, it does not pass it to the next handler, but rather releases an attached Byte Buffer with the payload instead. In order to use a custom handler, the handler must be added to the message processing pipeline. The following code snippet shows how to add a handler upon initialization:
```
.ChildHandler(new ActionChannelInitializer<ISocketChannel>(channel =>
{
    channel.Pipeline.AddLast(TlsHandler.Server(this.tlsCertificate));
    channel.Pipeline.AddLast(
        MqttEncoder.Instance,
        new MqttDecoder(...),
        new MyCustomHandler(...), // custom handler added here
        new MqttIotHubAdapter(...));
}))
```

Common tasks that can be addressed through custom handlers are compression, encryption, and other types of payload transformation. The protocol gateway includes a sample of a [compression handler](../host/ProtocolGateway.Host.Common/MqttPacketPayloadCompressionHandler.cs).

## Component Structure
The protocol gateway is built on top of [DotNetty](https://github.com/Azure/DotNetty/), an asynchronous event-driven network application framework.
The gateway uses DotNetty's components and extends the network communication over DotNetty's channels by adding message routing to and from Azure Iot Hub.

The protocol gateway is composed from the following components:
- **Bootstrapper** binds all other components to the execution environment (sourcing configuration, controlling lifetime).
- Core DotNetty components:
    - **Channel** is a representation of an I/O capable component, such as a network socket. For example, `TcpSocketChannel` is a channel built around a .NET socket connected over TCP.  
    - **Channel Handler** carries processing logic built around receiving, transforming, and forwarding I/O events and operations. For example, MQTT decoder is implemented as a Channel Handler that intercepts the incoming stream of bytes and sends forward-parsed MQTT packet objects. 
    - **Channel Pipeline** is a component which connects Channel Handlers in a chain, where inbound events are propagated forward while outbound operations are propagated backwards. For example, a Channel Pipeline can look like this: TLS Handler <-> MQTT Decoder <-> MQTT Encoder.
    - **Event Loop** is an API defining an execution model. All processing that happens in a Channel, Channel Pipeline, and Channel Handlers in the pipeline is carried out by an Executor.
    - **Event Loop Group** is a group of Event Loops that directs the assignment of Channels to Event Loops. For example, `MultithreadedEventLoopGroup` assigns newly established Channels evenly between predefined set of Executors.
    - **Byte Buffer** is a byte sequence providing convenient routines for simultaneous reading and writing.
    - **Byte Buffer Allocator** implements an allocation strategy for Byte Buffers.
    - **Server Bootstrap** is a DotNetty API for easy configuration of other DotNetty components on start. It allows the user to define what Byte Buffer Allocator should be used by all Channels, which Executors will be responsible for execution of event and operation handling logic associated with particular Channels, as well as what the Channel Pipeline will consist of for both listening (e.g. `TcpServerSocketChannel`) and connected Channels (e.g. `TcpSocketChannel`). 
- Specialized DotNetty components:
    - **TLS Channel Handler** provides an implementation of the TLS protocol. When added to a Channel Pipeline, it performs a TLS handshake without forwarding any data to the next Channel Handler. Once the TLS handshake is done, the Channel Handler performs decryption of inbound data and encryption of outbound data according to the TLS specification.
    - **MQTT Decoder and Encoder** are a pair of Channel Handlers that perform a decoding of a byte sequence into MQTT packet objects and encoding of MQTT packet objects into a byte sequence according to MQTT protocol specification. 
- **Mqtt Iot Hub Adapter** implements the MQTT protocol adapter which serves as a communication proxy. It depends on the following components: 
    - **Authentication Provider** performs authentication of new connections from devices based on the MQTT client ID, user name, and password supplied in the CONNECT packet and determines credentials to be used when connecting to IoT Hub on behalf of the device.
    - **Session State Manager** provides persistence for MQTT session state between connections.
    - **Message Delivery State Manager** provides persistence for delivery state tracking of MQTT QoS 2 messages (from IoT Hub to devices only).
    - **Topic Name Router** implements translation between MQTT topic names and IoT Hub endpoints and message properties.
    - **Request-Acknowledgement Pair Processor** encapsulates logic of ordered delivery of messages with acknowledgement. Optionally, it also supports timeout-based retransmission of unacknowledged messages.


## Scenarios
This section outlines the interactions between components of the protocol gateway for key MQTT message exchange scenarios. These interactions follow the framework defined by netty (and respectively DotNetty). They are reiterated in this guide in the specific context of the protocol gateway and MQTT.   

### Gateway Startup

1. The entry point (e.g. `WorkerRole`) initializes the components for accessing settings and the storage providers that are use by the MqttIotHubAdapter.
2. The entry point calls `Bootstrapper.RunAsync()`.
3. `Bootstrapper` determines the Event Loop Group and Byte Buffer Allocator settings based on the execution environment, specifically the number of CPU cores.
4. `Bootstrapper` creates and configures `ServerBootstrap`.
	1. `TcpServerSocketChannel` is defined as a listening Channel.
	2. `ActionChannelInitializer<ISocketChannel>` is added to the listening Channel's pipeline. It is configured to create the following pipeline upon creation of a new connected Channel: `TlsHandler` <-> `MqttEncoder` <-> `MqttDecoder` <-> `MqttIotHubAdapter`.
5. `Bootstrapper` calls `ServerBootstrap.BindAsync`, which does the following:
    - creates a new `TcpServerSocketChannel` object
    - registers the Channel with the Event Loop
    - adds `ServerBootstrapperAcceptor` to the Channel's pipeline
    - calls `BindAsync` on the Channel
6. `TcpServerSocketChannel` internally creates a `Socket` object and binds it to the specified address/port.

### Connection establishment

1. Once the new connection is accepted on the listening socket, a new `TcpSocketChannel` is created and dispatched by `TcpServerSocketChannel` to its pipeline and consequently to `ServerBootstrapAcceptor`.
2. `ServerBootstrapAcceptor` registers the channel with the Event Loop and adds `ActionChannelInitializer` to the Channel's pipeline per the configuration in step 1.
3. `ActionChannelInitializer` populates the channel's pipeline per configuration in step 4 above and then deletes itself from the pipeline.
4. When `TlsHandler` is added to the pipeline, it performs a TLS handshake and does not forward any data to the next channel handler in the pipeline.
5. When `MqttIotHubAdapter` is added to the pipeline, it requests a channel to read data. `TlsHandler` makes note of this request but does not pass it through until the TLS handshake is completed.
6. Once the TLS handshake is completed, `TlsHandler` lets through `IotHubAdapter`'s request to read data, and `TcpSocketChannel` reads data from the underlying socket.
7. When data is received on the socket, it is placed in a `ByteBuffer` and passed to the channel pipeline.
8. `TlsHandler` receives data and tries to parse a SSL frame. If there is less data than the expected frame length, it requests to read more data. Otherwise it decrypts the frame and passes a new Byte Buffer with the decrypted data forward to the next channel handler in the pipeline.
9. `MqttEncoder` is not engaged in processing inbound data, as it does not override any methods that correspond to inbound data/event processing.
10. `MqttDecoder` receives the decrypted data in the Byte Buffer and attempts to parse an MQTT packet. If there is not enough data in the Byte Buffer it received, it requests that the previous channel handler read more data. Once the MQTT packet is fully available for parsing, `MqttDecoder` creates an instance of an MQTT packet object, completes it with the parsed data, and passes it to the next channel handler in the pipeline. Per MQTT specification, a client is required to send a CONNECT packet first. We assume that `MqttDecoder` has parsed a CONNECT packet.
11. `MqttIotHubAdapter` receives a CONNECT packet.
12. `MqttIotHubAdapter` initiates a connection:
    - `MqttIotHubAdapter` calls `IDeviceIdentityProvider` to authenticate the device using the client ID, user name, and password from the CONNECT packet. `IDeviceIdentityProvider` also returns device information for establishing the connection to IoT Hub.
    - `MqttIotHubAdapter` initiates a connection to IoT Hub. As a result of a successful connection, an `IDeviceClient` instance is returned for future communication with IoT Hub on behalf of the device.
    - `MqttIotHubAdapter` calls `ISessionStateManager` to lookup the session state for the device. If the CleanSession flag is set to 1 in the CONNECT packet, `MqttIotHubAdapter` will also delete any existing session state.
    - `MqttIotHubAdapter` begins receiving messages from IoT Hub.
    - `MqttIotHubAdapter` writes out a CONNACK packet using the `WriteAsync` method on its context.
13. The CONNACK packet is passed to the previous channel handler in pipeline.
14. `MqttDecoder` does not override any methods intercepting outbound operations, so it doesn't perform any processing.
15. `MqttEncoder` encodes the CONNACK packet object into a Byte Buffer and passes it to the `TlsHandler`.
16. `TlsHandler` encrypts the CONNACK packet into a SSL frame and places it into a Byte Buffer. `TlsHandler` is the first handler in the pipeline so the Byte Buffer is passed to a channel to then be sent out over the network. Note that the actual sending happens only after the `Flush` method is called on the Channel.
17. If more packets have arrived while `MqttIotHubAdapter` has been processing the CONNECT packet, they are queued up and will be processed once a successful CONNACK is sent out to the device.

### Device Publishes a Message to Protocol Gateway

1. When a device sends a PUBLISH packet to the protocol gateway, it is processed through the same steps 7-10 as a CONNECT packet.
2. Once `MqttIotHubAdapter` receives a PUBLISH packet, it passes the packet to the `AsyncChannelPacketProcessor` asynchronously but sequentially.
3. `AsyncChannelPacketProcessor` calls back into `MqttIotHubAdapter.PublishToServerAsync` to process the packet.
4. `MqttIotHubAdapter` creates an Iot Hub message based on the PUBLISH packet.
5. `MqttIotHubAdapter` calls `ITopicNameRouter.TryMapTopicNameToRoute` to properly parse the data from the topic name of the PUBLISH packet.
6. `MqttIotHubAdapter` adds the parsed variables as message properties and sends it to IoT Hub through `IDeviceClient`, which was acquired during connection establishement in step 12 above.
7. If the QoS flag of the PUBLISH packet is set to 1 ("at least once"), `MqttIotHubAdapter` will send a PUBACK packet through the channel pipeline, which is similar to steps 13-16 above to process a CONNECT packet.

### Protocol gateway receives message from IoT Hub 
Once `MqttIotHubAdapter` has opened a receive link to IoT hub, it can receive a message at any time. When a message is received:

1. `MqttIotHubAdapter` calls the `ITopicNameRouter.TryMapRouteToTopicName` method. It prepares a topic name that will be used for the PUBLISH packet to the device.
2. `MqttIotHubAdapter` then matches the topic name against the list of subscriptions for the device. If the topic name does not match any subscription filter from the list, `MqttIotHubAdapter` rejects the message.
3. Based on the matched subscription and requested QoS on the message, `MqttIotHubAdapter` decides which QoS to use for the PUBLISH packet. 
4. If QoS=0,
    - `MqttIotHubAdapter` sends a PUBLISH packet through the pipeline (see steps 13-16 above for CONNECT processing)
    - At the same time `MqttIotHubAdater` deletes the message from IoT hub.
5. If QoS=1,
    - `MqttIotHubAdapter` posts a PUBLISH packet to `RequestAckPairProcessor` responsible for handling PUBLISH-PUBACK communication.
    - `RequestAckPairProcessor` sends PUBLISH packet through the pipeline (see steps 13-16 above for CONNECT processing).
    - At the same time `RequestAckPairProcessor` enqueues an entry with the message state in its internal ACK-pending queue. The ACK-pending queue is used for tracking messages in-flight that await on acknowledgement messages to arrive from the device.
    - Eventually (assuming normal operation), the PUBACK packet corresponding to the PUBLISH packet is received from the device and goes through the pipeline to `MqttIotHubAdapter`.
    - `MqttIotHubAdapter` dispatches the PUBACK packet to `RequestAckPairProcessor`.
    - `RequestAckPairProcessor` peeks at the top message from its ACK-pending queue, and if it does not match the received PUBACK packet, the PUBACK packet gets discarded and processing stops (the following steps are skipped).
    - `RequestAckPairProcessor` dequeues the message from the ACK-pending queue and calls `MqttIotHubAdapter` to complete acknowledgement.
    - `MqttIotHubAdapter` deletes the message from IoT hub.
5. If QoS=2,
    - `MqttIotHubAdapter` posts a PUBLISH packet to `RequestAckPairProcessor` responsible for handling PUBLISH-PUBREC communication.
    - `RequestAckPairProcessor` sends a PUBLISH packet through the pipeline (see steps 13-16 above for CONNECT processing).
    - At the same time the `RequestAckPairProcessor` enqueues an entry with the message state in its internal ACK-pending queue.
    - Eventually (assuming normal operation), the PUBREC packet corresponding to the PUBLISH packet is received from the device and gets through the pipeline to `MqttIotHubAdapter`.
    - `MqttIotHubAdapter` dispatches the PUBREC packet to `RequestAckPairProcessor`.
    - `RequestAckPairProcessor` peeks at the top message from its ACK-pending queue, and if it does not match the received PUBREC packet, the PUBREC packet gets discarded and processing stops (the following steps are skipped).
    - `RequestAckPairProcessor` dequeues the message from the ACK-pending queue and calls `MqttIotHubAdapter` to complete acknowledgement.
    - `MqttIotHubAdapter` calls `IQos2StatePersistenceProvider.SetMessageAsync` to persist the fact of acknowledged PUBLISH packet delivery.
    - `MqttIotHubAdapter` posts a PUBREL packet to the `RequestAckPairProcessor` responsible for handling PUBREL-PUBCOMP communication.
    - `RequestAckPairProcessor` sends the PUBREL packet through the pipeline (see steps 13-16 above for CONNECT processing).
    - At the same time the `RequestAckPairProcessor` enqueues an entry with the message state in its internal ACK-pending queue.
    - Eventually (assuming normal operation), the PUBCOMP packet corresponding to the PUBREL packet is received from the device and goes through the pipeline to `MqttIotHubAdapter`.
    - `MqttIotHubAdapter` dispatches the PUBCOMP packet to `RequestAckPairProcessor`.
    - `RequestAckPairProcessor` peeks at the top message from its ACK-pending queue, and if the message does not match the received PUBCOMP packet, the PUBCOMP packet gets discarded and processing stops (the following steps are skipped).
    - `RequestAckPairProcessor` dequeues the message from the ACK-pending queue and calls `MqttIotHubAdapter` to complete acknowledgement.
    - `MqttIotHubAdapter` deletes the message from IoT hub
    - `MqttIotHubAdapter` calls `IQos2StatePersistenceProvider.DeleteMessageAsync` to remove the QoS 2 delivery record.
