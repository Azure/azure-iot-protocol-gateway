namespace ProtocolGateway.Host.Fabric.FrontEnd.Configuration
{
    #region Using Clauses
    using System;
    #endregion

    public class MqttServiceConfiguration
    {
        /// <summary>
        /// The state provider used for MQTT QoS
        /// </summary>
        public QosStateType MqttQoSStateProvider { get; set; }

        /// <summary>
        /// The state provider used for MQTT QoS
        /// </summary>
        public QosStateType MqttQoS2StateProvider { get; set; }

        /// <summary>
        /// Maximum allowed number of ACK messages pending processing. Protocol gateway will stop reading data from the device connection once it reaches 
        /// MaxPendingInboundAcknowledgements and will restart only after the number of pending acknowledgements becomes lower than 
        /// MaxPendingInboundAcknowledgements. Default value: 16
        /// </summary>
        public int MaxPendingInboundAcknowledgements { get; set; }

        /// <summary>
        /// If specified, indicates timeout for acknowledgement. If an acknowledgement times out for any of the acknowledgement queues 
        /// (PUBACK, PUBREC, PUBCOMP), the device connection will be put in retransmission mode until all outstanding acknowledgements 
        /// are received. If not specified or is less than or equal to 00:00:00, acknowledgement is awaited without a timeout. Default value: 00:00:00
        /// </summary>
        public TimeSpan DeviceReceiveAckTimeout { get; set; }

        /// <summary>
        /// REQUIRED Maximum message size allowed for publishing from a device to the gateway. If a device publishes a bigger message, the protocol gateway 
        /// will close the connection. The max supported value is 262144 (256 KB). Default value: 262144
        /// </summary>
        public int MaxInboundMessageSize { get; set; }

        /// <summary>
        /// If specified, the time period after which an established TCP-connection will get closed, if a CONNECT packet has not been received. 
        /// If not specified or is less than or equal to 00:00:00, no timeout will be imposed on a CONNECT packet arrival. Default value: not set
        /// </summary>
        public TimeSpan ConnectArrivalTimeout { get; set; }

        /// <summary>
        /// If specified, and a device connects with KeepAlive=0 or a value that is higher than MaxKeepAliveTimeout, the connections keep alive timeout 
        /// will be set to MaxKeepAliveTimeout. If not specified or is less than or equal to 0, the KeepAlive value provided by the device will be used. 
        /// Default value: not set
        /// </summary>
        public TimeSpan MaxKeepAliveTimeout { get; set; }

        /// <summary>
        /// Protocol gateway will set a property named RetainProperty and a value of “True” on a message sent to IoT hub if the PUBLISH packet was 
        /// marked with RETAIN=1. Default value: mqtt-retain
        /// </summary>
        public string RetainPropertyName { get; set; }

        /// <summary>
        /// Protocol gateway will set a property named DupProperty and a value of “True” on a message sent to IoT hub if the PUBLISH packet was 
        /// marked with DUP=1. Default value: mqtt-dup
        /// </summary>
        public string DupPropertyName { get; set; }

        /// <summary>
        /// Indicates the name of the property on cloud-to-device messages that might be used to override the default QoS level for the device-bound 
        /// message processing. For device to cloud messages, this is the name of the property that indicates the QoS level of a message when received 
        /// from the device. Default value: mqtt-qos
        /// </summary>
        public string QoSPropertyName { get; set; }

        /// <summary>
        /// A template that is used to translate inbound Mqtt messages
        /// </summary>
        public string[] MqttInboundTemplates { get; set; }

        /// <summary>
        /// A template that is used to configure outbound Mqtt messages
        /// </summary>
        public string[] MqttOutboundTemplates { get; set; }

        /// <summary>
        /// Determines the type of MQTT QoS state store
        /// </summary>
        public enum QosStateType
        {
            AzureStorage,
            ServiceFabricStateful
        }
    }
}
