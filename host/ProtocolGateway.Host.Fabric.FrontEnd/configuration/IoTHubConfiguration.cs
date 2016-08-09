namespace ProtocolGateway.Host.Fabric.FrontEnd.Configuration
{
    #region Using Clauses
    using System;
    #endregion

    /// <summary>
    /// IoTHubClient configuration data
    /// </summary>
    public class IoTHubConfiguration
    {
        /// <summary>
        /// REQUIRED Connection string to IoT hub. Defines the connection parameters when connecting to IoT hub on behalf of devices. By default, 
        /// the protocol gateway will override the credentials from the connection string with the device credentials provided when a device connects 
        /// to the gateway. No default value.
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// Default Quality of Service to be used when publishing a message to a device. Can be overridden by adding a property on the message in 
        /// IoT hub with the name defined by the QoSProperty setting and the value of the desired QoS level. Supported values are: 0, 1, 2. 
        /// Default value: 1 (at least once)
        /// </summary>
        public int DefaultPublishToClientQoS { get; set; }

        /// <summary>
        /// Maximum allowed number of received messages pending processing. Protocol gateway will stop reading data from the device connection once 
        /// it reaches MaxPendingInboundMessages and will restart only after the number of pending messages becomes lower than MaxPendingInboundMessages. 
        /// Default value: 16
        /// </summary>
        public int MaxPendingInboundMessages { get; set; }

        /// <summary>
        /// Maximum allowed number of published device-bound messages pending acknowledgement. Protocol gateway will stop receiving messages from IoT hub 
        /// once it reaches MaxPendingOutboundMessages and will restart only after the number of messages pending acknowledgement becomes lower than 
        /// MaxPendingOutboundMessages. The number of messages is calculated as a sum of all unacknowledged messages: PUBLISH (QoS 1) pending PUBACK, 
        /// PUBLISH (QoS 2) pending PUBREC, and PUBREL pending PUBCOMP. Default value: 1
        /// </summary>
        public int MaxPendingOutboundMessages { get; set; }

        /// <summary>
        /// If specified, maximum number of attempts to deliver a device-bound message. Messages that reach the limit of allowed delivery attempts will be 
        /// rejected. If not specified or is less than or equal to 0, no maximum number of delivery attempts is imposed. Default value: -1
        /// </summary>
        public int MaxOutboundRetransmissionCount { get; set; }

        /// <summary>
        /// The maximum connection count to the IoT Hub 
        /// </summary>
        public int ConnectionPoolSize { get; set; }

        /// <summary>
        /// The connection timeout for a client connected to the IoT Hub
        /// </summary>
        public TimeSpan ConnectionIdleTimeout { get; set; }
    }
}
