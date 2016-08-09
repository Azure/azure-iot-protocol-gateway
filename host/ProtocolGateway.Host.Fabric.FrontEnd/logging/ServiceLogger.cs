namespace ProtocolGateway.Host.Fabric.FrontEnd.Logging
{
    #region Using Clauses
    using System;
    using System.Diagnostics.Tracing;
    using System.Fabric;
    using ProtocolGateway.Host.Fabric.FabricShared.Logging;

    #endregion

    /// <summary>
    /// An event source used initiating ETW events
    /// </summary>
    [EventSource(Name = "Microsoft-ProtocolGateway-Service")]
    public sealed class ServiceLogger : ServiceLoggerBase
    {
        #region Constructors
        /// <summary>
        /// Initializes a new instance of the type
        /// </summary>
        /// <param name="systemName">The name of the system the events are being logged for</param>
        /// <param name="nodeType">The node type that the logger is running on</param>
        /// <param name="nodeName">The name of the node that the logger is running on</param>
        /// <param name="serviceUri">The service Uri of the service the logger is in</param>
        /// <param name="applicationUri">The application Uri the logger is in</param>
        /// <param name="partitionId">The partition id that the logger is associated with</param>
        /// <param name="replicaOrInstanceId">The replica or instance id that the logger is associated with</param>
        public ServiceLogger(string systemName, string nodeType, string nodeName, string serviceUri, string applicationUri, Guid partitionId, long replicaOrInstanceId) :
            base(systemName, nodeType, nodeName, serviceUri, applicationUri, partitionId, replicaOrInstanceId)
        {
        }

        /// <summary>
        /// Initializes a new instance of the type
        /// </summary>
        /// <param name="systemName">The name of the system the events are being logged for</param>
        /// <param name="context">The Service Fabric reliable service context that the logger processes messages for</param>
        public ServiceLogger(string systemName, ServiceContext context) : base(systemName, context)
        {

        }
        #endregion
        #region Event Decorated Writers
        /// <summary>
        /// The method describing the verbose event for the ETW Manifest
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
        /// <param name="nodeType">The node type that the logger is running on</param>
        /// <param name="nodeName">The name of the node that the logger is running on</param>
        /// <param name="serviceUri">The service Uri of the service the logger is in</param>
        /// <param name="applicationUri">The application Uri the logger is in</param>
        /// <param name="partitionId">The partition id that the logger is associated with</param>
        /// <param name="replicaOrInstanceId">The replica or instance id that the logger is associated with</param>
        /// <param name="computerName">The name of the computer the event is being written from</param>
        /// <param name="processId">The process id that the event originated from</param>
        /// <param name="systemName">The solution or service name that is logging the message</param>
        /// <param name="componentName">The component within the clients solution that is logging the message</param>
        /// <param name="message">The message to log</param>
        /// <param name="errorMessage">The string representation of the exception that ocurred</param>
        /// <param name="memberName">The member that logged the message within the client code</param>
        /// <param name="fileName">The file name that logged the message within the client code</param>
        /// <param name="lineNumber">The line number in the file that logged the message within the client code</param>
        [Event(1, Keywords = Keywords.StandardMessage, Level = EventLevel.Verbose, Message = "{11}")]
        protected override void VerboseStandardMessage(Guid traceId, string nodeType, string nodeName, string serviceUri, string applicationUri, Guid partitionId, long replicaOrInstanceId, string computerName, int processId, string systemName, string componentName, string message, string errorMessage, string memberName, string fileName, int lineNumber)
        {
            if (this.IsEnabled(EventLevel.Verbose, Keywords.StandardMessage))
            {
                this.WriteStandardLogEvent(1, traceId, nodeType, nodeName, serviceUri, applicationUri, partitionId,
                    replicaOrInstanceId, computerName, processId, systemName, componentName, message, errorMessage,
                    memberName, fileName, lineNumber);
            }
        }

        /// <summary>
        /// The method describing the verbose event for the ETW Manifest
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
        /// <param name="nodeType">The node type that the logger is running on</param>
        /// <param name="nodeName">The name of the node that the logger is running on</param>
        /// <param name="serviceUri">The service Uri of the service the logger is in</param>
        /// <param name="applicationUri">The application Uri the logger is in</param>
        /// <param name="partitionId">The partition id that the logger is associated with</param>
        /// <param name="replicaOrInstanceId">The replica or instance id that the logger is associated with</param>
        /// <param name="computerName">The name of the computer the event is being written from</param>
        /// <param name="processId">The process id that the event originated from</param>
        /// <param name="systemName">The solution or service name that is logging the message</param>
        /// <param name="componentName">The component within the clients solution that is logging the message</param>
        /// <param name="message">The message to log</param>
        /// <param name="errorMessage">The string representation of the exception that ocurred</param>
        /// <param name="memberName">The member that logged the message within the client code</param>
        /// <param name="fileName">The file name that logged the message within the client code</param>
        /// <param name="lineNumber">The line number in the file that logged the message within the client code</param>
        [Event(2, Keywords = Keywords.StandardMessage, Level = EventLevel.Informational, Message = "{11}")]
        protected override void InformationalStandardMessage(Guid traceId, string nodeType, string nodeName, string serviceUri, string applicationUri, Guid partitionId, long replicaOrInstanceId, string computerName, int processId, string systemName, string componentName, string message, string errorMessage, string memberName, string fileName, int lineNumber)
        {
            if (this.IsEnabled(EventLevel.Informational, Keywords.StandardMessage))
            {
                this.WriteStandardLogEvent(2, traceId, nodeType, nodeName, serviceUri, applicationUri, partitionId,
                    replicaOrInstanceId, computerName, processId, systemName, componentName, message, errorMessage,
                    memberName, fileName, lineNumber);
            }
        }

        /// <summary>
        /// The method describing the verbose event for the ETW Manifest
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
        /// <param name="nodeType">The node type that the logger is running on</param>
        /// <param name="nodeName">The name of the node that the logger is running on</param>
        /// <param name="serviceUri">The service Uri of the service the logger is in</param>
        /// <param name="applicationUri">The application Uri the logger is in</param>
        /// <param name="partitionId">The partition id that the logger is associated with</param>
        /// <param name="replicaOrInstanceId">The replica or instance id that the logger is associated with</param>
        /// <param name="computerName">The name of the computer the event is being written from</param>
        /// <param name="processId">The process id that the event originated from</param>
        /// <param name="systemName">The solution or service name that is logging the message</param>
        /// <param name="componentName">The component within the clients solution that is logging the message</param>
        /// <param name="message">The message to log</param>
        /// <param name="errorMessage">The string representation of the exception that ocurred</param>
        /// <param name="memberName">The member that logged the message within the client code</param>
        /// <param name="fileName">The file name that logged the message within the client code</param>
        /// <param name="lineNumber">The line number in the file that logged the message within the client code</param>
        [Event(3, Keywords = Keywords.StandardMessage, Level = EventLevel.Warning, Message = "{11}")]
        protected override void WarningStandardMessage(Guid traceId, string nodeType, string nodeName, string serviceUri, string applicationUri, Guid partitionId, long replicaOrInstanceId, string computerName, int processId, string systemName, string componentName, string message, string errorMessage, string memberName, string fileName, int lineNumber)
        {
            if (this.IsEnabled(EventLevel.Warning, Keywords.StandardMessage))
            {
                this.WriteStandardLogEvent(3, traceId, nodeType, nodeName, serviceUri, applicationUri, partitionId,
                    replicaOrInstanceId, computerName, processId, systemName, componentName, message, errorMessage,
                    memberName, fileName, lineNumber);
            }
        }

        /// <summary>
        /// The method describing the verbose event for the ETW Manifest
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
        /// <param name="nodeType">The node type that the logger is running on</param>
        /// <param name="nodeName">The name of the node that the logger is running on</param>
        /// <param name="serviceUri">The service Uri of the service the logger is in</param>
        /// <param name="applicationUri">The application Uri the logger is in</param>
        /// <param name="partitionId">The partition id that the logger is associated with</param>
        /// <param name="replicaOrInstanceId">The replica or instance id that the logger is associated with</param>
        /// <param name="computerName">The name of the computer the event is being written from</param>
        /// <param name="processId">The process id that the event originated from</param>
        /// <param name="systemName">The solution or service name that is logging the message</param>
        /// <param name="componentName">The component within the clients solution that is logging the message</param>
        /// <param name="message">The message to log</param>
        /// <param name="errorMessage">The string representation of the exception that ocurred</param>
        /// <param name="memberName">The member that logged the message within the client code</param>
        /// <param name="fileName">The file name that logged the message within the client code</param>
        /// <param name="lineNumber">The line number in the file that logged the message within the client code</param>
        [Event(4, Keywords = Keywords.StandardMessage, Level = EventLevel.Error, Message = "{11}")]
        protected override void ErrorStandardMessage(Guid traceId, string nodeType, string nodeName, string serviceUri, string applicationUri, Guid partitionId, long replicaOrInstanceId, string computerName, int processId, string systemName, string componentName, string message, string errorMessage, string memberName, string fileName, int lineNumber)
        {
            if (this.IsEnabled(EventLevel.Error, Keywords.StandardMessage))
            {
                this.WriteStandardLogEvent(4, traceId, nodeType, nodeName, serviceUri, applicationUri, partitionId,
                    replicaOrInstanceId, computerName, processId, systemName, componentName, message, errorMessage,
                    memberName, fileName, lineNumber);
            }
        }

        /// <summary>
        /// The method describing the verbose event for the ETW Manifest
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
        /// <param name="nodeType">The node type that the logger is running on</param>
        /// <param name="nodeName">The name of the node that the logger is running on</param>
        /// <param name="serviceUri">The service Uri of the service the logger is in</param>
        /// <param name="applicationUri">The application Uri the logger is in</param>
        /// <param name="partitionId">The partition id that the logger is associated with</param>
        /// <param name="replicaOrInstanceId">The replica or instance id that the logger is associated with</param>
        /// <param name="computerName">The name of the computer the event is being written from</param>
        /// <param name="processId">The process id that the event originated from</param>
        /// <param name="systemName">The solution or service name that is logging the message</param>
        /// <param name="componentName">The component within the clients solution that is logging the message</param>
        /// <param name="message">The message to log</param>
        /// <param name="errorMessage">The string representation of the exception that ocurred</param>
        /// <param name="memberName">The member that logged the message within the client code</param>
        /// <param name="fileName">The file name that logged the message within the client code</param>
        /// <param name="lineNumber">The line number in the file that logged the message within the client code</param>
        [Event(5, Keywords = Keywords.StandardMessage, Level = EventLevel.Critical, Message = "{11}")]
        protected override void CriticalStandardMessage(Guid traceId, string nodeType, string nodeName, string serviceUri, string applicationUri, Guid partitionId, long replicaOrInstanceId, string computerName, int processId, string systemName, string componentName, string message, string errorMessage, string memberName, string fileName, int lineNumber)
        {
            if (this.IsEnabled(EventLevel.Critical, Keywords.StandardMessage))
            {
                this.WriteStandardLogEvent(5, traceId, nodeType, nodeName, serviceUri, applicationUri, partitionId,
                    replicaOrInstanceId, computerName, processId, systemName, componentName, message, errorMessage,
                    memberName, fileName, lineNumber);
            }
        }

        /// <summary>
        /// The method describing the verbose event for the ETW Manifest
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
        /// <param name="nodeType">The node type that the logger is running on</param>
        /// <param name="nodeName">The name of the node that the logger is running on</param>
        /// <param name="serviceUri">The service Uri of the service the logger is in</param>
        /// <param name="applicationUri">The application Uri the logger is in</param>
        /// <param name="partitionId">The partition id that the logger is associated with</param>
        /// <param name="replicaOrInstanceId">The replica or instance id that the logger is associated with</param>
        /// <param name="computerName">The name of the computer the event is being written from</param>
        /// <param name="processId">The process id that the event originated from</param>
        /// <param name="systemName">The solution or service name that is logging the message</param>
        /// <param name="componentName">The component within the clients solution that is logging the message</param>
        /// <param name="message">The message to log</param>
        /// <param name="timing">The time associated with the message, typically in ms</param>
        /// <param name="errorMessage">The string representation of the exception that ocurred</param>
        /// <param name="memberName">The member that logged the message within the client code</param>
        /// <param name="fileName">The file name that logged the message within the client code</param>
        /// <param name="lineNumber">The line number in the file that logged the message within the client code</param>
        [Event(6, Keywords = Keywords.TimingMessage, Level = EventLevel.Verbose, Message = "{11}")]
        protected override void VerboseTimingMessage(Guid traceId, string nodeType, string nodeName, string serviceUri, string applicationUri, Guid partitionId, long replicaOrInstanceId, string computerName, int processId, string systemName, string componentName, string message, double timing, string errorMessage, string memberName, string fileName, int lineNumber)
        {
            if (this.IsEnabled(EventLevel.Verbose, Keywords.TimingMessage))
            {
                this.WriteTimingLogEvent(6, traceId, nodeType, nodeName, serviceUri, applicationUri, partitionId,
                    replicaOrInstanceId, computerName, processId, systemName, componentName, message, timing,
                    errorMessage, memberName, fileName, lineNumber);
            }
        }

        /// <summary>
        /// The method describing the verbose event for the ETW Manifest
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
        /// <param name="nodeType">The node type that the logger is running on</param>
        /// <param name="nodeName">The name of the node that the logger is running on</param>
        /// <param name="serviceUri">The service Uri of the service the logger is in</param>
        /// <param name="applicationUri">The application Uri the logger is in</param>
        /// <param name="partitionId">The partition id that the logger is associated with</param>
        /// <param name="replicaOrInstanceId">The replica or instance id that the logger is associated with</param>
        /// <param name="computerName">The name of the computer the event is being written from</param>
        /// <param name="processId">The process id that the event originated from</param>
        /// <param name="systemName">The solution or service name that is logging the message</param>
        /// <param name="componentName">The component within the clients solution that is logging the message</param>
        /// <param name="message">The message to log</param>
        /// <param name="timing">The time associated with the message, typically in ms</param>
        /// <param name="errorMessage">The string representation of the exception that ocurred</param>
        /// <param name="memberName">The member that logged the message within the client code</param>
        /// <param name="fileName">The file name that logged the message within the client code</param>
        /// <param name="lineNumber">The line number in the file that logged the message within the client code</param>
        [Event(7, Keywords = Keywords.TimingMessage, Level = EventLevel.Informational, Message = "{11}")]
        protected override void InformationalTimingMessage(Guid traceId, string nodeType, string nodeName, string serviceUri, string applicationUri, Guid partitionId, long replicaOrInstanceId, string computerName, int processId, string systemName, string componentName, string message, double timing, string errorMessage, string memberName, string fileName, int lineNumber)
        {
            if (this.IsEnabled(EventLevel.Informational, Keywords.TimingMessage))
            {
                this.WriteTimingLogEvent(7, traceId, nodeType, nodeName, serviceUri, applicationUri, partitionId,
                    replicaOrInstanceId, computerName, processId, systemName, componentName, message, timing,
                    errorMessage, memberName, fileName, lineNumber);
            }
        }

        /// <summary>
        /// The method describing the verbose event for the ETW Manifest
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
        /// <param name="nodeType">The node type that the logger is running on</param>
        /// <param name="nodeName">The name of the node that the logger is running on</param>
        /// <param name="serviceUri">The service Uri of the service the logger is in</param>
        /// <param name="applicationUri">The application Uri the logger is in</param>
        /// <param name="partitionId">The partition id that the logger is associated with</param>
        /// <param name="replicaOrInstanceId">The replica or instance id that the logger is associated with</param>
        /// <param name="computerName">The name of the computer the event is being written from</param>
        /// <param name="processId">The process id that the event originated from</param>
        /// <param name="systemName">The solution or service name that is logging the message</param>
        /// <param name="componentName">The component within the clients solution that is logging the message</param>
        /// <param name="timing">The time associated with the message, typically in ms</param>
        /// <param name="message">The message to log</param>
        /// <param name="errorMessage">The string representation of the exception that ocurred</param>
        /// <param name="memberName">The member that logged the message within the client code</param>
        /// <param name="fileName">The file name that logged the message within the client code</param>
        /// <param name="lineNumber">The line number in the file that logged the message within the client code</param>
        [Event(8, Keywords = Keywords.TimingMessage, Level = EventLevel.Warning, Message = "{11}")]
        protected override void WarningTimingMessage(Guid traceId, string nodeType, string nodeName, string serviceUri, string applicationUri, Guid partitionId, long replicaOrInstanceId, string computerName, int processId, string systemName, string componentName, string message, double timing, string errorMessage, string memberName, string fileName, int lineNumber)
        {
            if (this.IsEnabled(EventLevel.Warning, Keywords.TimingMessage))
            {
                this.WriteTimingLogEvent(8, traceId, nodeType, nodeName, serviceUri, applicationUri, partitionId,
                    replicaOrInstanceId, computerName, processId, systemName, componentName, message, timing,
                    errorMessage, memberName, fileName, lineNumber);
            }
        }

        /// <summary>
        /// The method describing the verbose event for the ETW Manifest
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
        /// <param name="nodeType">The node type that the logger is running on</param>
        /// <param name="nodeName">The name of the node that the logger is running on</param>
        /// <param name="serviceUri">The service Uri of the service the logger is in</param>
        /// <param name="applicationUri">The application Uri the logger is in</param>
        /// <param name="partitionId">The partition id that the logger is associated with</param>
        /// <param name="replicaOrInstanceId">The replica or instance id that the logger is associated with</param>
        /// <param name="computerName">The name of the computer the event is being written from</param>
        /// <param name="processId">The process id that the event originated from</param>
        /// <param name="systemName">The solution or service name that is logging the message</param>
        /// <param name="componentName">The component within the clients solution that is logging the message</param>
        /// <param name="timing">The time associated with the message, typically in ms</param>
        /// <param name="message">The message to log</param>
        /// <param name="errorMessage">The string representation of the exception that ocurred</param>
        /// <param name="memberName">The member that logged the message within the client code</param>
        /// <param name="fileName">The file name that logged the message within the client code</param>
        /// <param name="lineNumber">The line number in the file that logged the message within the client code</param>
        [Event(9, Keywords = Keywords.TimingMessage, Level = EventLevel.Error, Message = "{11}")]
        protected override void ErrorTimingMessage(Guid traceId, string nodeType, string nodeName, string serviceUri, string applicationUri, Guid partitionId, long replicaOrInstanceId, string computerName, int processId, string systemName, string componentName, string message, double timing, string errorMessage, string memberName, string fileName, int lineNumber)
        {
            if (this.IsEnabled(EventLevel.Error, Keywords.TimingMessage))
            {
                this.WriteTimingLogEvent(9, traceId, nodeType, nodeName, serviceUri, applicationUri, partitionId,
                    replicaOrInstanceId, computerName, processId, systemName, componentName, message, timing,
                    errorMessage, memberName, fileName, lineNumber);
            }
        }

        /// <summary>
        /// The method describing the verbose event for the ETW Manifest
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
        /// <param name="nodeType">The node type that the logger is running on</param>
        /// <param name="nodeName">The name of the node that the logger is running on</param>
        /// <param name="serviceUri">The service Uri of the service the logger is in</param>
        /// <param name="applicationUri">The application Uri the logger is in</param>
        /// <param name="partitionId">The partition id that the logger is associated with</param>
        /// <param name="replicaOrInstanceId">The replica or instance id that the logger is associated with</param>
        /// <param name="computerName">The name of the computer the event is being written from</param>
        /// <param name="processId">The process id that the event originated from</param>
        /// <param name="systemName">The solution or service name that is logging the message</param>
        /// <param name="componentName">The component within the clients solution that is logging the message</param>
        /// <param name="message">The message to log</param>
        /// <param name="timing">The time associated with the message, typically in ms</param>
        /// <param name="errorMessage">The string representation of the exception that ocurred</param>
        /// <param name="memberName">The member that logged the message within the client code</param>
        /// <param name="fileName">The file name that logged the message within the client code</param>
        /// <param name="lineNumber">The line number in the file that logged the message within the client code</param>
        [Event(10, Keywords = Keywords.TimingMessage, Level = EventLevel.Critical, Message = "{11}")]
        protected override void CriticalTimingMessage(Guid traceId, string nodeType, string nodeName, string serviceUri, string applicationUri, Guid partitionId, long replicaOrInstanceId, string computerName, int processId, string systemName, string componentName, string message, double timing, string errorMessage, string memberName, string fileName, int lineNumber)
        {
            if (this.IsEnabled(EventLevel.Critical, Keywords.TimingMessage))
            {
                this.WriteTimingLogEvent(10, traceId, nodeType, nodeName, serviceUri, applicationUri, partitionId,
                    replicaOrInstanceId, computerName, processId, systemName, componentName, message, timing,
                    errorMessage, memberName, fileName, lineNumber);
            }
        }
        #endregion
        #region Service Method Decorated Writers
        /// <summary>
        /// The method describing successful creation of a Service Fabric service instance
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
        /// <param name="nodeType">The node type that the logger is running on</param>
        /// <param name="nodeName">The name of the node that the logger is running on</param>
        /// <param name="serviceUri">The service Uri of the service the logger is in</param>
        /// <param name="applicationUri">The application Uri the logger is in</param>
        /// <param name="partitionId">The partition id that the logger is associated with</param>
        /// <param name="replicaOrInstanceId">The replica or instance id that the logger is associated with</param>
        /// <param name="computerName">The name of the computer the event is being written from</param>
        /// <param name="processId">The process id that the event originated from</param>
        /// <param name="systemName">The solution or service name that is logging the message</param>
        /// <param name="componentName">The component within the clients solution that is logging the message</param>
        /// <param name="message">The message to log</param>
        /// <param name="errorMessage">The string representation of the exception that ocurred</param>
        /// <param name="memberName">The member that logged the message within the client code</param>
        /// <param name="fileName">The file name that logged the message within the client code</param>
        /// <param name="lineNumber">The line number in the file that logged the message within the client code</param>
        [Event(11, Keywords = Keywords.ServiceInstanceConstructed, Level = EventLevel.Informational, Message = "{11}")]
        protected override void ServiceInstanceConstructedSuccess(Guid traceId, string nodeType, string nodeName, string serviceUri, string applicationUri, Guid partitionId, long replicaOrInstanceId, string computerName, int processId, string systemName, string componentName, string message, string errorMessage, string memberName, string fileName, int lineNumber)
        {
            if (this.IsEnabled(EventLevel.Informational, Keywords.ServiceInstanceConstructed))
            {
                this.WriteStandardLogEvent(11, traceId, nodeType, nodeName, serviceUri, applicationUri, partitionId,
                    replicaOrInstanceId, computerName, processId, systemName, componentName, message, errorMessage,
                    memberName, fileName, lineNumber);
            }
        }

        /// <summary>
        /// The method describing successful creation of a Service Fabric service instance
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
        /// <param name="nodeType">The node type that the logger is running on</param>
        /// <param name="nodeName">The name of the node that the logger is running on</param>
        /// <param name="serviceUri">The service Uri of the service the logger is in</param>
        /// <param name="applicationUri">The application Uri the logger is in</param>
        /// <param name="partitionId">The partition id that the logger is associated with</param>
        /// <param name="replicaOrInstanceId">The replica or instance id that the logger is associated with</param>
        /// <param name="computerName">The name of the computer the event is being written from</param>
        /// <param name="processId">The process id that the event originated from</param>
        /// <param name="systemName">The solution or service name that is logging the message</param>
        /// <param name="componentName">The component within the clients solution that is logging the message</param>
        /// <param name="message">The message to log</param>
        /// <param name="errorMessage">The string representation of the exception that ocurred</param>
        /// <param name="memberName">The member that logged the message within the client code</param>
        /// <param name="fileName">The file name that logged the message within the client code</param>
        /// <param name="lineNumber">The line number in the file that logged the message within the client code</param>
        [Event(12, Keywords = Keywords.ServiceInstanceConstructed, Level = EventLevel.Error, Message = "{11}")]
        protected override void ServiceInstanceConstructedError(Guid traceId, string nodeType, string nodeName, string serviceUri, string applicationUri, Guid partitionId, long replicaOrInstanceId, string computerName, int processId, string systemName, string componentName, string message, string errorMessage, string memberName, string fileName, int lineNumber)
        {
            if (this.IsEnabled(EventLevel.Error, Keywords.ServiceInstanceConstructed))
            {
                this.WriteStandardLogEvent(12, traceId, nodeType, nodeName, serviceUri, applicationUri, partitionId,
                    replicaOrInstanceId, computerName, processId, systemName, componentName, message, errorMessage,
                    memberName, fileName, lineNumber);
            }
        }

        /// <summary>
        /// The method indicating a Service Fabric service lifecycle transition or event has occurred 
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
        /// <param name="nodeType">The node type that the logger is running on</param>
        /// <param name="nodeName">The name of the node that the logger is running on</param>
        /// <param name="serviceUri">The service Uri of the service the logger is in</param>
        /// <param name="applicationUri">The application Uri the logger is in</param>
        /// <param name="partitionId">The partition id that the logger is associated with</param>
        /// <param name="replicaOrInstanceId">The replica or instance id that the logger is associated with</param>
        /// <param name="computerName">The name of the computer the event is being written from</param>
        /// <param name="processId">The process id that the event originated from</param>
        /// <param name="systemName">The solution or service name that is logging the message</param>
        /// <param name="componentName">The component within the clients solution that is logging the message</param>
        /// <param name="message">The message to log</param>
        /// <param name="errorMessage">The string representation of the exception that ocurred</param>
        /// <param name="memberName">The member that logged the message within the client code</param>
        /// <param name="fileName">The file name that logged the message within the client code</param>
        /// <param name="lineNumber">The line number in the file that logged the message within the client code</param>
        [Event(13, Keywords = Keywords.ServiceInstanceLifecycle, Level = EventLevel.Informational, Message = "{11}")]
        protected override void ServiceLifeycleEvent(Guid traceId, string nodeType, string nodeName, string serviceUri, string applicationUri, Guid partitionId, long replicaOrInstanceId, string computerName, int processId, string systemName, string componentName, string message, string errorMessage, string memberName, string fileName, int lineNumber)
        {
            if (this.IsEnabled(EventLevel.Informational, Keywords.ServiceInstanceLifecycle))
            {
                this.WriteStandardLogEvent(13, traceId, nodeType, nodeName, serviceUri, applicationUri, partitionId,
                    replicaOrInstanceId, computerName, processId, systemName, componentName, message, errorMessage,
                    memberName, fileName, lineNumber);
            }
        }

        /// <summary>
        /// The method indicating a Service Fabric partition had a data loss event 
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
        /// <param name="nodeType">The node type that the logger is running on</param>
        /// <param name="nodeName">The name of the node that the logger is running on</param>
        /// <param name="serviceUri">The service Uri of the service the logger is in</param>
        /// <param name="applicationUri">The application Uri the logger is in</param>
        /// <param name="partitionId">The partition id that the logger is associated with</param>
        /// <param name="replicaOrInstanceId">The replica or instance id that the logger is associated with</param>
        /// <param name="computerName">The name of the computer the event is being written from</param>
        /// <param name="processId">The process id that the event originated from</param>
        /// <param name="systemName">The solution or service name that is logging the message</param>
        /// <param name="componentName">The component within the clients solution that is logging the message</param>
        /// <param name="message">The message to log</param>
        /// <param name="errorMessage">The string representation of the exception that ocurred</param>
        /// <param name="memberName">The member that logged the message within the client code</param>
        /// <param name="fileName">The file name that logged the message within the client code</param>
        /// <param name="lineNumber">The line number in the file that logged the message within the client code</param>
        [Event(14, Keywords = Keywords.PartitionDataLoss, Level = EventLevel.Warning, Message = "{11}")]
        protected override void PartitionDataLossEvent(Guid traceId, string nodeType, string nodeName, string serviceUri, string applicationUri, Guid partitionId, long replicaOrInstanceId, string computerName, int processId, string systemName, string componentName, string message, string errorMessage, string memberName, string fileName, int lineNumber)
        {
            if (this.IsEnabled(EventLevel.Warning, Keywords.PartitionDataLoss))
            {
                this.WriteStandardLogEvent(14, traceId, nodeType, nodeName, serviceUri, applicationUri, partitionId,
                    replicaOrInstanceId, computerName, processId, systemName, componentName, message, errorMessage,
                    memberName, fileName, lineNumber);
            }
        }

        /// <summary>
        /// The method indicating a Service Fabric configuration change occurred 
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
        /// <param name="nodeType">The node type that the logger is running on</param>
        /// <param name="nodeName">The name of the node that the logger is running on</param>
        /// <param name="serviceUri">The service Uri of the service the logger is in</param>
        /// <param name="applicationUri">The application Uri the logger is in</param>
        /// <param name="partitionId">The partition id that the logger is associated with</param>
        /// <param name="replicaOrInstanceId">The replica or instance id that the logger is associated with</param>
        /// <param name="computerName">The name of the computer the event is being written from</param>
        /// <param name="processId">The process id that the event originated from</param>
        /// <param name="systemName">The solution or service name that is logging the message</param>
        /// <param name="componentName">The component within the clients solution that is logging the message</param>
        /// <param name="message">The message to log</param>
        /// <param name="errorMessage">The string representation of the exception that ocurred</param>
        /// <param name="memberName">The member that logged the message within the client code</param>
        /// <param name="fileName">The file name that logged the message within the client code</param>
        /// <param name="lineNumber">The line number in the file that logged the message within the client code</param>
        [Event(15, Keywords = Keywords.ConfigurationChanged, Level = EventLevel.Warning, Message = "{11}")]
        protected override void ConfigurationChangeEvent(Guid traceId, string nodeType, string nodeName, string serviceUri, string applicationUri, Guid partitionId, long replicaOrInstanceId, string computerName, int processId, string systemName, string componentName, string message, string errorMessage, string memberName, string fileName, int lineNumber)
        {
            if (this.IsEnabled(EventLevel.Warning, Keywords.ConfigurationChanged))
            {
                this.WriteStandardLogEvent(15, traceId, nodeType, nodeName, serviceUri, applicationUri, partitionId,
                    replicaOrInstanceId, computerName, processId, systemName, componentName, message, errorMessage,
                    memberName, fileName, lineNumber);
            }
        }

        /// <summary>
        /// The method indicating a service request was made, stopped etc.
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
        /// <param name="nodeType">The node type that the logger is running on</param>
        /// <param name="nodeName">The name of the node that the logger is running on</param>
        /// <param name="serviceUri">The service Uri of the service the logger is in</param>
        /// <param name="applicationUri">The application Uri the logger is in</param>
        /// <param name="partitionId">The partition id that the logger is associated with</param>
        /// <param name="replicaOrInstanceId">The replica or instance id that the logger is associated with</param>
        /// <param name="computerName">The name of the computer the event is being written from</param>
        /// <param name="processId">The process id that the event originated from</param>
        /// <param name="systemName">The solution or service name that is logging the message</param>
        /// <param name="componentName">The component within the clients solution that is logging the message</param>
        /// <param name="message">The message to log</param>
        /// <param name="timing">The time associated with the message, typically in ms</param>
        /// <param name="errorMessage">The string representation of the exception that ocurred</param>
        /// <param name="memberName">The member that logged the message within the client code</param>
        /// <param name="fileName">The file name that logged the message within the client code</param>
        /// <param name="lineNumber">The line number in the file that logged the message within the client code</param>
        [Event(16, Keywords = Keywords.ServiceRequest, Level = EventLevel.Verbose, Message = "{11}")]
        protected override void ServiceRequestEvent(Guid traceId, string nodeType, string nodeName, string serviceUri, string applicationUri, Guid partitionId, long replicaOrInstanceId, string computerName, int processId, string systemName, string componentName, string message, double timing, string errorMessage, string memberName, string fileName, int lineNumber)
        {
            if (this.IsEnabled(EventLevel.Verbose, Keywords.ServiceRequest))
            {
                this.WriteTimingLogEvent(16, traceId, nodeType, nodeName, serviceUri, applicationUri, partitionId,
                    replicaOrInstanceId, computerName, processId, systemName, componentName, message, timing,
                    errorMessage, memberName, fileName, lineNumber);
            }
        }

        /// <summary>
        /// The method indicating a service request failed etc.
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
        /// <param name="nodeType">The node type that the logger is running on</param>
        /// <param name="nodeName">The name of the node that the logger is running on</param>
        /// <param name="serviceUri">The service Uri of the service the logger is in</param>
        /// <param name="applicationUri">The application Uri the logger is in</param>
        /// <param name="partitionId">The partition id that the logger is associated with</param>
        /// <param name="replicaOrInstanceId">The replica or instance id that the logger is associated with</param>
        /// <param name="computerName">The name of the computer the event is being written from</param>
        /// <param name="processId">The process id that the event originated from</param>
        /// <param name="systemName">The solution or service name that is logging the message</param>
        /// <param name="componentName">The component within the clients solution that is logging the message</param>
        /// <param name="message">The message to log</param>
        /// <param name="errorMessage">The string representation of the exception that ocurred</param>
        /// <param name="memberName">The member that logged the message within the client code</param>
        /// <param name="fileName">The file name that logged the message within the client code</param>
        /// <param name="lineNumber">The line number in the file that logged the message within the client code</param>
        [Event(17, Keywords = Keywords.ServiceRequest, Level = EventLevel.Error, Message = "{11}")]
        protected override void ServiceRequestFailedEvent(Guid traceId, string nodeType, string nodeName, string serviceUri, string applicationUri, Guid partitionId, long replicaOrInstanceId, string computerName, int processId, string systemName, string componentName, string message, string errorMessage, string memberName, string fileName, int lineNumber)
        {
            if (this.IsEnabled(EventLevel.Error, Keywords.ServiceRequest))
            {
                this.WriteStandardLogEvent(17, traceId, nodeType, nodeName, serviceUri, applicationUri, partitionId,
                    replicaOrInstanceId, computerName, processId, systemName, componentName, message, errorMessage,
                    memberName, fileName, lineNumber);
            }
        }
        #endregion
        #region Keywords
        // Event keywords can be used to categorize events. 
        // Each keyword is a bit flag. A single event can be associated with multiple keywords (via EventAttribute.Keywords property).
        // Keywords must be defined as a public class named 'Keywords' inside EventSource that uses them.
        public static class Keywords
        {
            /// <summary>
            /// A standard log event
            /// </summary>
            public const EventKeywords StandardMessage = (EventKeywords)1L;

            /// <summary>
            /// A timing log event that requires service fabric metadata for context
            /// </summary>
            public const EventKeywords TimingMessage = (EventKeywords)2L;

            /// <summary>
            /// Service fabric service instance is being initialized
            /// </summary>
            public const EventKeywords ServiceInstanceConstructed = (EventKeywords)4L;

            /// <summary>
            /// Service fabric lifecycle events
            /// </summary>
            public const EventKeywords ServiceInstanceLifecycle = (EventKeywords)8L;

            /// <summary>
            /// Service fabric data loss
            /// </summary>
            public const EventKeywords PartitionDataLoss = (EventKeywords)16L;

            /// <summary>
            /// Service fabric configuration changed
            /// </summary>
            public const EventKeywords ConfigurationChanged = (EventKeywords)32L;

            /// <summary>
            /// Service fabric requests to services
            /// </summary>
            public const EventKeywords ServiceRequest = (EventKeywords)64L;
        }
        #endregion
    }
}
