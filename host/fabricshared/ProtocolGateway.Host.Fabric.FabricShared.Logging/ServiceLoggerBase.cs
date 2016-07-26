namespace ProtocolGateway.Host.Fabric.FabricShared.Logging
{
    #region Using Clauses
    using System;
    using System.Diagnostics;
    using System.Diagnostics.Tracing;
    using System.Fabric;
    using System.Runtime.CompilerServices;
    using System.Security;
    using System.Threading.Tasks;
    #endregion

    /// <summary>
    /// An event source base class used initiating ETW events. A typical attribute to apply to the class is 
    /// [EventSource(Name = "MyCompany-Logger-Service")]
    /// </summary>
    [SuppressUnmanagedCodeSecurity]
    public abstract class ServiceLoggerBase : EventSource, IServiceLogger
    {
        #region Constructors
        /// <summary>
        /// A static constructor only implemented to work around a problem where the ETW activities are not initialized immediately.  This is expected to be fixed in .NET Framework 4.6.2 when
        /// it can be removed
        /// </summary>
        static ServiceLoggerBase()
        {
            Task.Run(() => { }).Wait();
        }

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
        protected ServiceLoggerBase(string systemName, string nodeType, string nodeName, string serviceUri, string applicationUri, Guid partitionId, long replicaOrInstanceId)
        {
            this.nodeType = nodeType;
            this.nodeName = nodeName;
            this.serviceUri = serviceUri;
            this.applicationUri = applicationUri;
            this.partitionId = partitionId;
            this.replicaOrInstanceId = replicaOrInstanceId;
            this.systemName = systemName;
        }

        /// <summary>
        /// Initializes a new instance of the type
        /// </summary>
        /// <param name="systemName">The name of the system the events are being logged for</param>
        /// <param name="context">The Service Fabric reliable service context that the logger processes messages for</param>
        protected ServiceLoggerBase(string systemName, ServiceContext context) : this(systemName, context.NodeContext.NodeType, context.NodeContext.NodeName, context.ServiceName.AbsoluteUri,
                                                                   context.CodePackageActivationContext.ApplicationName, context.PartitionId, context.ReplicaOrInstanceId)
        {

        }
        #endregion
        #region Variables
        /// <summary>
        /// The node type that the logger is running on
        /// </summary>
        readonly string nodeType;

        /// <summary>
        /// The name of the node that the logger is running on
        /// </summary>
        readonly string nodeName;

        /// <summary>
        /// The service Uri of the service the logger is in
        /// </summary>
        readonly string serviceUri;

        /// <summary>
        /// The application Uri the logger is in
        /// </summary>
        readonly string applicationUri;

        /// <summary>
        /// The partition id that the logger is associated with
        /// </summary>
        readonly Guid partitionId;

        /// <summary>
        /// The replica or instance id that the logger is associated with
        /// </summary>
        readonly long replicaOrInstanceId;

        /// <summary>
        /// The name of the system the data is being logged for
        /// </summary>
        readonly string systemName;
        #endregion
        #region Public Logging Methods
        /// <summary>
        /// Writes a standard critical log messages for non service fabric clients
        /// </summary>
        /// <param name="message">The message to write and if a string type with parameters present it will write it as the format string</param>
        /// <param name="parameters">The ordered list of parameters to be inserted into the placeholders of the format string</param>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
        /// <param name="componentName">The component within the clients solution that is logging the message</param>
        /// <param name="error">The error that occurred resulting in this log message being written</param>
        /// <param name="memberName">The member that logged the message within the client code</param>
        /// <param name="fileName">The file name that logged the message within the client code</param>
        /// <param name="lineNumber">The line number in the file that logged the message within the client code</param>
        [NonEvent]
        public void Critical<T>(Guid traceId, string componentName, T message, object[] parameters = null, Exception error = null, [CallerMemberName] string memberName = null, [CallerFilePath] string fileName = null, [CallerLineNumber] int lineNumber = 0)
        {
            this.CriticalStandardMessage(traceId, this.nodeType, this.nodeName, this.serviceUri, this.applicationUri, this.partitionId, this.replicaOrInstanceId, Environment.MachineName, Process.GetCurrentProcess().Id, this.systemName, componentName, GetMessageString(message, parameters), error?.Flatten(), memberName, fileName, lineNumber);
        }

        /// <summary>
        /// Writes a timing critical log messages for non service fabric clients
        /// </summary>
        /// <param name="message">The message to write and if a string type with parameters present it will write it as the format string</param>
        /// <param name="parameters">The ordered list of parameters to be inserted into the placeholders of the format string</param>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
        /// <param name="componentName">The component within the clients solution that is logging the message</param>
        /// <param name="timing">The time associated with the message, typically in ms</param>
        /// <param name="error">The error that occurred resulting in this log message being written</param>
        /// <param name="memberName">The member that logged the message within the client code</param>
        /// <param name="fileName">The file name that logged the message within the client code</param>
        /// <param name="lineNumber">The line number in the file that logged the message within the client code</param>
        [NonEvent]
        public void Critical<T>(Guid traceId, string componentName, double timing, T message, object[] parameters = null, Exception error = null, [CallerMemberName] string memberName = null, [CallerFilePath] string fileName = null, [CallerLineNumber] int lineNumber = 0)
        {
            this.CriticalTimingMessage(traceId, this.nodeType, this.nodeName, this.serviceUri, this.applicationUri, this.partitionId, this.replicaOrInstanceId, Environment.MachineName, Process.GetCurrentProcess().Id, this.systemName, componentName, GetMessageString(message, parameters), timing, error?.Flatten(), memberName, fileName, lineNumber);
        }

        /// <summary>
        /// Writes a standard error log messages for non service fabric clients
        /// </summary>
        /// <param name="message">The message to write and if a string type with parameters present it will write it as the format string</param>
        /// <param name="parameters">The ordered list of parameters to be inserted into the placeholders of the format string</param>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
        /// <param name="componentName">The component within the clients solution that is logging the message</param>
        /// <param name="error">The error that occurred resulting in this log message being written</param>
        /// <param name="memberName">The member that logged the message within the client code</param>
        /// <param name="fileName">The file name that logged the message within the client code</param>
        /// <param name="lineNumber">The line number in the file that logged the message within the client code</param>
        [NonEvent]
        public void Error<T>(Guid traceId, string componentName, T message, object[] parameters = null, Exception error = null, [CallerMemberName] string memberName = null, [CallerFilePath] string fileName = null, [CallerLineNumber] int lineNumber = 0)
        {
            this.ErrorStandardMessage(traceId, this.nodeType, this.nodeName, this.serviceUri, this.applicationUri, this.partitionId, this.replicaOrInstanceId, Environment.MachineName, Process.GetCurrentProcess().Id, this.systemName, componentName, GetMessageString(message, parameters), error?.Flatten(), memberName, fileName, lineNumber);
        }

        /// <summary>
        /// Writes a timing error log messages for non service fabric clients
        /// </summary>
        /// <param name="message">The message to write and if a string type with parameters present it will write it as the format string</param>
        /// <param name="parameters">The ordered list of parameters to be inserted into the placeholders of the format string</param>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
        /// <param name="componentName">The component within the clients solution that is logging the message</param>
        /// <param name="timing">The time associated with the message, typically in ms</param>
        /// <param name="error">The error that occurred resulting in this log message being written</param>
        /// <param name="memberName">The member that logged the message within the client code</param>
        /// <param name="fileName">The file name that logged the message within the client code</param>
        /// <param name="lineNumber">The line number in the file that logged the message within the client code</param>
        [NonEvent]
        public void Error<T>(Guid traceId, string componentName, double timing, T message, object[] parameters = null, Exception error = null, [CallerMemberName] string memberName = null, [CallerFilePath] string fileName = null, [CallerLineNumber] int lineNumber = 0)
        {
            this.ErrorTimingMessage(traceId, this.nodeType, this.nodeName, this.serviceUri, this.applicationUri, this.partitionId, this.replicaOrInstanceId, Environment.MachineName, Process.GetCurrentProcess().Id, this.systemName, componentName, GetMessageString(message, parameters), timing, error?.Flatten(), memberName, fileName, lineNumber);
        }

        /// <summary>
        /// Writes a standard informational log messages for non service fabric clients
        /// </summary>
        /// <param name="message">The message to write and if a string type with parameters present it will write it as the format string</param>
        /// <param name="parameters">The ordered list of parameters to be inserted into the placeholders of the format string</param>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
        /// <param name="componentName">The component within the clients solution that is logging the message</param>
        /// <param name="error">The error that occurred resulting in this log message being written</param>
        /// <param name="memberName">The member that logged the message within the client code</param>
        /// <param name="fileName">The file name that logged the message within the client code</param>
        /// <param name="lineNumber">The line number in the file that logged the message within the client code</param>
        [NonEvent]
        public void Informational<T>(Guid traceId, string componentName, T message, object[] parameters = null, Exception error = null, [CallerMemberName] string memberName = null, [CallerFilePath] string fileName = null, [CallerLineNumber] int lineNumber = 0)
        {
            this.InformationalStandardMessage(traceId, this.nodeType, this.nodeName, this.serviceUri, this.applicationUri, this.partitionId, this.replicaOrInstanceId, Environment.MachineName, Process.GetCurrentProcess().Id, this.systemName, componentName, GetMessageString(message, parameters), error?.Flatten(), memberName, fileName, lineNumber);
        }

        /// <summary>
        /// Writes a timing informational log messages for non service fabric clients
        /// </summary>
        /// <param name="message">The message to write and if a string type with parameters present it will write it as the format string</param>
        /// <param name="parameters">The ordered list of parameters to be inserted into the placeholders of the format string</param>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
        /// <param name="componentName">The component within the clients solution that is logging the message</param>
        /// <param name="timing">The time associated with the message, typically in ms</param>
        /// <param name="error">The error that occurred resulting in this log message being written</param>
        /// <param name="memberName">The member that logged the message within the client code</param>
        /// <param name="fileName">The file name that logged the message within the client code</param>
        /// <param name="lineNumber">The line number in the file that logged the message within the client code</param>
        [NonEvent]
        public void Informational<T>(Guid traceId, string componentName, double timing, T message, object[] parameters = null, Exception error = null, [CallerMemberName] string memberName = null, [CallerFilePath] string fileName = null, [CallerLineNumber] int lineNumber = 0)
        {
            this.InformationalTimingMessage(traceId, this.nodeType, this.nodeName, this.serviceUri, this.applicationUri, this.partitionId, this.replicaOrInstanceId, Environment.MachineName, Process.GetCurrentProcess().Id, this.systemName, componentName, GetMessageString(message, parameters), timing, error?.Flatten(), memberName, fileName, lineNumber);
        }

        /// <summary>
        /// Writes a standard verbose log messages for non service fabric clients
        /// </summary>
        /// <param name="message">The message to write and if a string type with parameters present it will write it as the format string</param>
        /// <param name="parameters">The ordered list of parameters to be inserted into the placeholders of the format string</param>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
        /// <param name="componentName">The component within the clients solution that is logging the message</param>
        /// <param name="error">The error that occurred resulting in this log message being written</param>
        /// <param name="memberName">The member that logged the message within the client code</param>
        /// <param name="fileName">The file name that logged the message within the client code</param>
        /// <param name="lineNumber">The line number in the file that logged the message within the client code</param>
        [NonEvent]
        public void Verbose<T>(Guid traceId, string componentName, T message, object[] parameters = null, Exception error = null, [CallerMemberName] string memberName = null, [CallerFilePath] string fileName = null, [CallerLineNumber] int lineNumber = 0)
        {
            this.VerboseStandardMessage(traceId, this.nodeType, this.nodeName, this.serviceUri, this.applicationUri, this.partitionId, this.replicaOrInstanceId, Environment.MachineName, Process.GetCurrentProcess().Id, this.systemName, componentName, GetMessageString(message, parameters), error?.Flatten(), memberName, fileName, lineNumber);
        }

        /// <summary>
        /// Writes a timing verbose log messages for non service fabric clients
        /// </summary>
        /// <param name="message">The message to write and if a string type with parameters present it will write it as the format string</param>
        /// <param name="parameters">The ordered list of parameters to be inserted into the placeholders of the format string</param>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
        /// <param name="componentName">The component within the clients solution that is logging the message</param>
        /// <param name="timing">The time associated with the message, typically in ms</param>
        /// <param name="error">The error that occurred resulting in this log message being written</param>
        /// <param name="memberName">The member that logged the message within the client code</param>
        /// <param name="fileName">The file name that logged the message within the client code</param>
        /// <param name="lineNumber">The line number in the file that logged the message within the client code</param>
        [NonEvent]
        public void Verbose<T>(Guid traceId, string componentName, double timing, T message, object[] parameters = null, Exception error = null, [CallerMemberName] string memberName = null, [CallerFilePath] string fileName = null, [CallerLineNumber] int lineNumber = 0)
        {
            this.VerboseTimingMessage(traceId, this.nodeType, this.nodeName, this.serviceUri, this.applicationUri, this.partitionId, this.replicaOrInstanceId, Environment.MachineName, Process.GetCurrentProcess().Id, this.systemName, componentName, GetMessageString(message, parameters), timing, error?.Flatten(), memberName, fileName, lineNumber);
        }

        /// <summary>
        /// Writes a standard warning log messages for non service fabric clients
        /// </summary>
        /// <param name="message">The message to write and if a string type with parameters present it will write it as the format string</param>
        /// <param name="parameters">The ordered list of parameters to be inserted into the placeholders of the format string</param>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
        /// <param name="componentName">The component within the clients solution that is logging the message</param>
        /// <param name="error">The error that occurred resulting in this log message being written</param>
        /// <param name="memberName">The member that logged the message within the client code</param>
        /// <param name="fileName">The file name that logged the message within the client code</param>
        /// <param name="lineNumber">The line number in the file that logged the message within the client code</param>
        [NonEvent]
        public void Warning<T>(Guid traceId, string componentName, T message, object[] parameters = null, Exception error = null, [CallerMemberName] string memberName = null, [CallerFilePath] string fileName = null, [CallerLineNumber] int lineNumber = 0)
        {
            this.WarningStandardMessage(traceId, this.nodeType, this.nodeName, this.serviceUri, this.applicationUri, this.partitionId, this.replicaOrInstanceId, Environment.MachineName, Process.GetCurrentProcess().Id, this.systemName, componentName, GetMessageString(message, parameters), error?.Flatten(), memberName, fileName, lineNumber);
        }

        /// <summary>
        /// Writes a timing warning log messages for non service fabric clients
        /// </summary>
        /// <param name="message">The message to write and if a string type with parameters present it will write it as the format string</param>
        /// <param name="parameters">The ordered list of parameters to be inserted into the placeholders of the format string</param>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
        /// <param name="componentName">The component within the clients solution that is logging the message</param>
        /// <param name="timing">The time associated with the message, typically in ms</param>
        /// <param name="error">The error that occurred resulting in this log message being written</param>
        /// <param name="memberName">The member that logged the message within the client code</param>
        /// <param name="fileName">The file name that logged the message within the client code</param>
        /// <param name="lineNumber">The line number in the file that logged the message within the client code</param>
        [NonEvent]
        public void Warning<T>(Guid traceId, string componentName, double timing, T message, object[] parameters = null, Exception error = null, [CallerMemberName] string memberName = null, [CallerFilePath] string fileName = null, [CallerLineNumber] int lineNumber = 0)
        {
            this.WarningTimingMessage(traceId, this.nodeType, this.nodeName, this.serviceUri, this.applicationUri, this.partitionId, this.replicaOrInstanceId, Environment.MachineName, Process.GetCurrentProcess().Id, this.systemName, componentName, GetMessageString(message, parameters), timing, error?.Flatten(), memberName, fileName, lineNumber);
        }
        #endregion
        #region Event Decorated Writers
        /// <summary>
        /// The method describing the verbose event for the ETW Manifest. Typical attribute is as follows
        /// [Event(1, Keywords = Keywords.StandardMessage, Level = EventLevel.Verbose, Message = "{11}")]
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
        protected abstract void VerboseStandardMessage(Guid traceId, string nodeType, string nodeName, string serviceUri,
            string applicationUri, Guid partitionId, long replicaOrInstanceId, string computerName, int processId,
            string systemName, string componentName, string message, string errorMessage, string memberName,
            string fileName, int lineNumber);

        /// <summary>
        /// The method describing the verbose event for the ETW Manifest. Typical attribute is as follows
        /// [Event(2, Keywords = Keywords.StandardMessage, Level = EventLevel.Informational, Message = "{11}")]
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
        protected abstract void InformationalStandardMessage(Guid traceId, string nodeType, string nodeName,
            string serviceUri, string applicationUri, Guid partitionId, long replicaOrInstanceId, string computerName,
            int processId, string systemName, string componentName, string message, string errorMessage,
            string memberName, string fileName, int lineNumber);

        /// <summary>
        /// The method describing the verbose event for the ETW Manifest. Typical attribute is as follows
        /// [Event(3, Keywords = Keywords.StandardMessage, Level = EventLevel.Warning, Message = "{11}")]
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
        protected abstract void WarningStandardMessage(Guid traceId, string nodeType, string nodeName, string serviceUri,
            string applicationUri, Guid partitionId, long replicaOrInstanceId, string computerName, int processId,
            string systemName, string componentName, string message, string errorMessage, string memberName,
            string fileName, int lineNumber);

        /// <summary>
        /// The method describing the verbose event for the ETW Manifest. Typical attribute is as follows
        /// [Event(4, Keywords = Keywords.StandardMessage, Level = EventLevel.Error, Message = "{11}")]
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
        protected abstract void ErrorStandardMessage(Guid traceId, string nodeType, string nodeName, string serviceUri,
            string applicationUri, Guid partitionId, long replicaOrInstanceId, string computerName, int processId,
            string systemName, string componentName, string message, string errorMessage, string memberName,
            string fileName, int lineNumber);

        /// <summary>
        /// The method describing the verbose event for the ETW Manifest. Typical attribute is as follows
        /// [Event(5, Keywords = Keywords.StandardMessage, Level = EventLevel.Critical, Message = "{11}")]
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
        protected abstract void CriticalStandardMessage(Guid traceId, string nodeType, string nodeName,
            string serviceUri, string applicationUri, Guid partitionId, long replicaOrInstanceId, string computerName,
            int processId, string systemName, string componentName, string message, string errorMessage,
            string memberName, string fileName, int lineNumber);

        /// <summary>
        /// The method describing the verbose event for the ETW Manifest. Typical attribute is as follows
        /// [Event(6, Keywords = Keywords.StandardMessage, Level = EventLevel.Verbose, Message = "{11}")]
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
        protected abstract void VerboseTimingMessage(Guid traceId, string nodeType, string nodeName, string serviceUri,
            string applicationUri, Guid partitionId, long replicaOrInstanceId, string computerName, int processId,
            string systemName, string componentName, string message, double timing, string errorMessage,
            string memberName, string fileName, int lineNumber);

        /// <summary>
        /// The method describing the verbose event for the ETW Manifest. Typical attribute is as follows
        /// [Event(7, Keywords = Keywords.StandardMessage, Level = EventLevel.Informational, Message = "{11}")]
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
        protected abstract void InformationalTimingMessage(Guid traceId, string nodeType, string nodeName,
            string serviceUri, string applicationUri, Guid partitionId, long replicaOrInstanceId, string computerName,
            int processId, string systemName, string componentName, string message, double timing, string errorMessage,
            string memberName, string fileName, int lineNumber);

        /// <summary>
        /// The method describing the verbose event for the ETW Manifest. Typical attribute is as follows
        /// [Event(8, Keywords = Keywords.StandardMessage, Level = EventLevel.Warning, Message = "{11}")]
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
        protected abstract void WarningTimingMessage(Guid traceId, string nodeType, string nodeName, string serviceUri,
            string applicationUri, Guid partitionId, long replicaOrInstanceId, string computerName, int processId,
            string systemName, string componentName, string message, double timing, string errorMessage,
            string memberName, string fileName, int lineNumber);

        /// <summary>
        /// The method describing the verbose event for the ETW Manifest. Typical attribute is as follows
        /// [Event(9, Keywords = Keywords.StandardMessage, Level = EventLevel.Error, Message = "{11}")]
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
        protected abstract void ErrorTimingMessage(Guid traceId, string nodeType, string nodeName, string serviceUri,
            string applicationUri, Guid partitionId, long replicaOrInstanceId, string computerName, int processId,
            string systemName, string componentName, string message, double timing, string errorMessage,
            string memberName, string fileName, int lineNumber);

        /// <summary>
        /// The method describing the verbose event for the ETW Manifest. Typical attribute is as follows
        /// [Event(10, Keywords = Keywords.StandardMessage, Level = EventLevel.Critical, Message = "{11}")]
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
        protected abstract void CriticalTimingMessage(Guid traceId, string nodeType, string nodeName, string serviceUri,
            string applicationUri, Guid partitionId, long replicaOrInstanceId, string computerName, int processId,
            string systemName, string componentName, string message, double timing, string errorMessage,
            string memberName, string fileName, int lineNumber);
        #endregion
        #region Private Event Writers
        /// <summary>
        /// Performs an unsafe write of the log event for performance
        /// </summary>
        /// <param name="eventId">The id of the event being logged</param>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
        /// <param name="fabricNodeType">The node type that the logger is running on</param>
        /// <param name="fabricNodeName">The name of the node that the logger is running on</param>
        /// <param name="fabricServiceUri">The service Uri of the service the logger is in</param>
        /// <param name="fabricApplicationUri">The application Uri the logger is in</param>
        /// <param name="fabricPartitionId">The partition id that the logger is associated with</param>
        /// <param name="fabricReplicaOrInstanceId">The replica or instance id that the logger is associated with</param>
        /// <param name="computerName">The name of the computer the event is being written from</param>
        /// <param name="processId">The process id that the event originated from</param>
        /// <param name="logSystemName">The solution or service name that is logging the message</param>
        /// <param name="componentName">The component within the clients solution that is logging the message</param>
        /// <param name="message">The message to log</param>
        /// <param name="errorMessage">The string representation of the exception that ocurred</param>
        /// <param name="memberName">The member that logged the message within the client code</param>
        /// <param name="fileName">The file name that logged the message within the client code</param>
        /// <param name="lineNumber">The line number in the file that logged the message within the client code</param>
        [NonEvent]
        [SuppressUnmanagedCodeSecurity]
        protected unsafe void WriteStandardLogEvent(int eventId, Guid traceId, string fabricNodeType, string fabricNodeName, string fabricServiceUri, string fabricApplicationUri, Guid fabricPartitionId, long fabricReplicaOrInstanceId, string computerName, int processId, string logSystemName, string componentName, string message, string errorMessage, string memberName, string fileName, int lineNumber)
        {
            const int ArgumentCount = 16;
            const string NullString = "";

            if (logSystemName == null) logSystemName = NullString;
            if (componentName == null) componentName = NullString;
            if (message == null) message = NullString;
            if (errorMessage == null) errorMessage = NullString;
            if (fileName == null) fileName = NullString;
            if (memberName == null) memberName = NullString;
            if (fabricNodeType == null) fabricNodeType = NullString;
            if (fabricServiceUri == null) fabricServiceUri = NullString;
            if (fabricApplicationUri == null) fabricApplicationUri = NullString;
            if (fabricNodeName == null) fabricNodeName = NullString;

            fixed (char* pMessage = message, pMemberName = memberName, pFileName = fileName, pSystemName = logSystemName, pComponentName = componentName, pErrorMessage = errorMessage,
                pComputerName = computerName, pNodeType = fabricNodeType, pNodeName = fabricNodeName, pServiceUri = fabricServiceUri, pApplicationUri = fabricApplicationUri)
            {
                EventData* eventData = stackalloc EventData[ArgumentCount];

                eventData[0] = new EventData { DataPointer = (IntPtr)(&traceId), Size = sizeof(Guid) };
                eventData[1] = new EventData { DataPointer = (IntPtr)pNodeType, Size = this.SizeInBytes(fabricNodeType) };
                eventData[2] = new EventData { DataPointer = (IntPtr)pNodeName, Size = this.SizeInBytes(fabricNodeName) };
                eventData[3] = new EventData { DataPointer = (IntPtr)pServiceUri, Size = this.SizeInBytes(fabricServiceUri) };
                eventData[4] = new EventData { DataPointer = (IntPtr)pApplicationUri, Size = this.SizeInBytes(fabricApplicationUri) };
                eventData[5] = new EventData { DataPointer = (IntPtr)(&fabricPartitionId), Size = sizeof(Guid) };
                eventData[6] = new EventData { DataPointer = (IntPtr)(&fabricReplicaOrInstanceId), Size = sizeof(long) };
                eventData[7] = new EventData { DataPointer = (IntPtr)pComputerName, Size = this.SizeInBytes(computerName) };
                eventData[8] = new EventData { DataPointer = (IntPtr)(&processId), Size = sizeof(int) };
                eventData[9] = new EventData { DataPointer = (IntPtr)pSystemName, Size = this.SizeInBytes(logSystemName) };
                eventData[10] = new EventData { DataPointer = (IntPtr)pComponentName, Size = this.SizeInBytes(componentName) };
                eventData[11] = new EventData { DataPointer = (IntPtr)pMessage, Size = this.SizeInBytes(message) };
                eventData[12] = new EventData { DataPointer = (IntPtr)pErrorMessage, Size = this.SizeInBytes(errorMessage) };
                eventData[13] = new EventData { DataPointer = (IntPtr)pMemberName, Size = this.SizeInBytes(memberName) };
                eventData[14] = new EventData { DataPointer = (IntPtr)pFileName, Size = this.SizeInBytes(fileName) };
                eventData[15] = new EventData { DataPointer = (IntPtr)(&lineNumber), Size = sizeof(int) };

                this.WriteEventCore(eventId, ArgumentCount, eventData);
            }
        }

        /// <summary>
        /// Performs an unsafe write of the log event for performance
        /// </summary>
        /// <param name="eventId">The id of the event being logged</param>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
        /// <param name="fabricNodeType">The node type that the logger is running on</param>
        /// <param name="fabricNodeName">The name of the node that the logger is running on</param>
        /// <param name="fabricServiceUri">The service Uri of the service the logger is in</param>
        /// <param name="fabricApplicationUri">The application Uri the logger is in</param>
        /// <param name="fabricPartitionId">The partition id that the logger is associated with</param>
        /// <param name="fabricReplicaOrInstanceId">The replica or instance id that the logger is associated with</param>
        /// <param name="computerName">The name of the computer the event is being written from</param>
        /// <param name="processId">The process id that the event originated from</param>
        /// <param name="logSystemName">The solution or service name that is logging the message</param>
        /// <param name="componentName">The component within the clients solution that is logging the message</param>
        /// <param name="message">The message to log</param>
        /// <param name="timing">The timing associated with the event being logged, typically in ms</param>
        /// <param name="errorMessage">The string representation of the exception that ocurred</param>
        /// <param name="memberName">The member that logged the message within the client code</param>
        /// <param name="fileName">The file name that logged the message within the client code</param>
        /// <param name="lineNumber">The line number in the file that logged the message within the client code</param>
        [NonEvent]
        [SuppressUnmanagedCodeSecurity]
        protected unsafe void WriteTimingLogEvent(int eventId, Guid traceId, string fabricNodeType, string fabricNodeName, string fabricServiceUri, string fabricApplicationUri, Guid fabricPartitionId, long fabricReplicaOrInstanceId, string computerName, int processId, string logSystemName, string componentName, string message, double timing, string errorMessage, string memberName, string fileName, int lineNumber)
        {
            const int ArgumentCount = 17;
            const string NullString = "";

            if (logSystemName == null) logSystemName = NullString;
            if (componentName == null) componentName = NullString;
            if (message == null) message = NullString;
            if (errorMessage == null) errorMessage = NullString;
            if (fileName == null) fileName = NullString;
            if (memberName == null) memberName = NullString;
            if (fabricNodeType == null) fabricNodeType = NullString;
            if (fabricServiceUri == null) fabricServiceUri = NullString;
            if (fabricApplicationUri == null) fabricApplicationUri = NullString;
            if (fabricNodeName == null) fabricNodeName = NullString;

            fixed (char* pMessage = message, pMemberName = memberName, pFileName = fileName, pSystemName = logSystemName, pComponentName = componentName, pErrorMessage = errorMessage,
                pComputerName = computerName, pNodeType = fabricNodeType, pNodeName = fabricNodeName, pServiceUri = fabricServiceUri, pApplicationUri = fabricApplicationUri)
            {
                EventData* eventData = stackalloc EventData[ArgumentCount];

                eventData[0] = new EventData { DataPointer = (IntPtr)(&traceId), Size = sizeof(Guid) };
                eventData[1] = new EventData { DataPointer = (IntPtr)pNodeType, Size = this.SizeInBytes(fabricNodeType) };
                eventData[2] = new EventData { DataPointer = (IntPtr)pNodeName, Size = this.SizeInBytes(fabricNodeName) };
                eventData[3] = new EventData { DataPointer = (IntPtr)pServiceUri, Size = this.SizeInBytes(fabricServiceUri) };
                eventData[4] = new EventData { DataPointer = (IntPtr)pApplicationUri, Size = this.SizeInBytes(fabricApplicationUri) };
                eventData[5] = new EventData { DataPointer = (IntPtr)(&fabricPartitionId), Size = sizeof(Guid) };
                eventData[6] = new EventData { DataPointer = (IntPtr)(&fabricReplicaOrInstanceId), Size = sizeof(long) };
                eventData[7] = new EventData { DataPointer = (IntPtr)pComputerName, Size = this.SizeInBytes(computerName) };
                eventData[8] = new EventData { DataPointer = (IntPtr)(&processId), Size = sizeof(int) };
                eventData[9] = new EventData { DataPointer = (IntPtr)pSystemName, Size = this.SizeInBytes(logSystemName) };
                eventData[10] = new EventData { DataPointer = (IntPtr)pComponentName, Size = this.SizeInBytes(componentName) };
                eventData[11] = new EventData { DataPointer = (IntPtr)pMessage, Size = this.SizeInBytes(message) };
                eventData[12] = new EventData { DataPointer = (IntPtr)(&timing), Size = sizeof(double) };
                eventData[13] = new EventData { DataPointer = (IntPtr)pErrorMessage, Size = this.SizeInBytes(errorMessage) };
                eventData[14] = new EventData { DataPointer = (IntPtr)pMemberName, Size = this.SizeInBytes(memberName) };
                eventData[15] = new EventData { DataPointer = (IntPtr)pFileName, Size = this.SizeInBytes(fileName) };
                eventData[16] = new EventData { DataPointer = (IntPtr)(&lineNumber), Size = sizeof(int) };

                this.WriteEventCore(eventId, ArgumentCount, eventData);
            }
        }
        #endregion
        #region Service Method Decorated Writers
        /// <summary>
        /// The method describing successful creation of a Service Fabric service instance. Typical attribute is as follows
        /// [Event(11, Keywords = Keywords.ServiceInstanceConstructed, Level = EventLevel.Informational, Message = "{11}")]
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
        protected abstract void ServiceInstanceConstructedSuccess(Guid traceId, string nodeType, string nodeName,
            string serviceUri, string applicationUri, Guid partitionId, long replicaOrInstanceId, string computerName,
            int processId, string systemName, string componentName, string message, string errorMessage,
            string memberName, string fileName, int lineNumber);

        /// <summary>
        /// The method describing successful creation of a Service Fabric service instance. Typical attribute is as follows
        /// [Event(12, Keywords = Keywords.ServiceInstanceConstructed, Level = EventLevel.Error, Message = "{11}")]
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

        protected abstract void ServiceInstanceConstructedError(Guid traceId, string nodeType, string nodeName,
            string serviceUri, string applicationUri, Guid partitionId, long replicaOrInstanceId, string computerName,
            int processId, string systemName, string componentName, string message, string errorMessage,
            string memberName, string fileName, int lineNumber);

        /// <summary>
        /// The method indicating a Service Fabric service lifecycle transition or event has occurred. Typical attribute is as follows 
        /// [Event(13, Keywords = Keywords.ServiceInstanceLifecycle, Level = EventLevel.Informational, Message = "{11}")]
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

        protected abstract void ServiceLifeycleEvent(Guid traceId, string nodeType, string nodeName, string serviceUri,
            string applicationUri, Guid partitionId, long replicaOrInstanceId, string computerName, int processId,
            string systemName, string componentName, string message, string errorMessage, string memberName,
            string fileName, int lineNumber);

        /// <summary>
        /// The method indicating a Service Fabric partition had a data loss event. Typical attribute is as follows 
        /// [Event(14, Keywords = Keywords.PartitionDataLoss, Level = EventLevel.Warning, Message = "{11}")]
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

        protected abstract void PartitionDataLossEvent(Guid traceId, string nodeType, string nodeName, string serviceUri,
            string applicationUri, Guid partitionId, long replicaOrInstanceId, string computerName, int processId,
            string systemName, string componentName, string message, string errorMessage, string memberName,
            string fileName, int lineNumber);

        /// <summary>
        /// The method indicating a Service Fabric configuration change occurred. Typical attribute is as follows 
        /// [Event(15, Keywords = Keywords.ConfigurationChanged, Level = EventLevel.Warning, Message = "{11}")]
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

        protected abstract void ConfigurationChangeEvent(Guid traceId, string nodeType, string nodeName,
            string serviceUri, string applicationUri, Guid partitionId, long replicaOrInstanceId, string computerName,
            int processId, string systemName, string componentName, string message, string errorMessage,
            string memberName, string fileName, int lineNumber);

        /// <summary>
        /// The method indicating a service request was made, stopped etc. Typical attribute is as follows
        /// [Event(16, Keywords = Keywords.ServiceRequest, Level = EventLevel.Verbose, Message = "{11}")]
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

        protected abstract void ServiceRequestEvent(Guid traceId, string nodeType, string nodeName, string serviceUri,
            string applicationUri, Guid partitionId, long replicaOrInstanceId, string computerName, int processId,
            string systemName, string componentName, string message, double timing, string errorMessage,
            string memberName, string fileName, int lineNumber);

        /// <summary>
        /// The method indicating a service request failed etc. Typical attribute is as follows
        /// [Event(17, Keywords = Keywords.ServiceRequest, Level = EventLevel.Error, Message = "{11}")]
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

        protected abstract void ServiceRequestFailedEvent(Guid traceId, string nodeType, string nodeName,
            string serviceUri, string applicationUri, Guid partitionId, long replicaOrInstanceId, string computerName,
            int processId, string systemName, string componentName, string message, string errorMessage,
            string memberName, string fileName, int lineNumber);
        #endregion
        #region Service Specific Methods
        /// <summary>
        /// A service instance was started for the service type.
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
        /// <param name="componentName">The component within the clients solution that is logging the message</param>
        /// <param name="serviceTypeName">Name of the service type.</param>
        /// <param name="error">The error that occurred resulting in this log message being written</param>
        /// <param name="memberName">The member that logged the message within the client code</param>
        /// <param name="fileName">The file name that logged the message within the client code</param>
        /// <param name="lineNumber">The line number in the file that logged the message within the client code</param>
        [NonEvent]
        public void ServiceInstanceConstructed(Guid traceId, string componentName, string serviceTypeName, Exception error = null,
                     [CallerMemberName] string memberName = null, [CallerFilePath] string fileName = null, [CallerLineNumber] int lineNumber = 0)
        {
            if (error == null)
            {
                this.ServiceInstanceConstructedSuccess(traceId, this.nodeType, this.nodeName, this.serviceUri, this.applicationUri, this.partitionId, this.replicaOrInstanceId, Environment.MachineName, Process.GetCurrentProcess().Id, this.systemName, componentName, "Service instance constructed.", null, memberName, fileName, lineNumber);
            }
            else
            {
                this.ServiceInstanceConstructedError(traceId, this.nodeType, this.nodeName, this.serviceUri, this.applicationUri, this.partitionId, this.replicaOrInstanceId, Environment.MachineName, Process.GetCurrentProcess().Id, this.systemName, componentName, "Service instance had an error while being constructed.", error.Flatten(), memberName, fileName, lineNumber);
            }
        }

        /// <summary>
        /// Start request event.
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
        /// <param name="componentName">The component within the clients solution that is logging the message</param>
        /// <param name="serviceTypeName">Name of the service type.</param>
        /// <param name="requestTypeName">Name of the request type.</param>
        /// <param name="error">The error that occurred resulting in this log message being written</param>
        /// <param name="memberName">The member that logged the message within the client code</param>
        /// <param name="fileName">The file name that logged the message within the client code</param>
        /// <param name="lineNumber">The line number in the file that logged the message within the client code</param>
        [NonEvent]
        public void ServiceRequestStart(Guid traceId, string componentName, string serviceTypeName, string requestTypeName, Exception error = null,
                     [CallerMemberName] string memberName = null, [CallerFilePath] string fileName = null, [CallerLineNumber] int lineNumber = 0)
        {
            this.ServiceRequestEvent(traceId, this.nodeType, this.nodeName, this.serviceUri, this.applicationUri, this.partitionId, this.replicaOrInstanceId, Environment.MachineName, Process.GetCurrentProcess().Id, this.systemName, componentName, $"Service request started requestTypeName { requestTypeName }.", 0.0, error?.Flatten(), memberName, fileName, lineNumber);
        }

        /// <summary>
        /// Stop request event.
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
        /// <param name="componentName">The component within the clients solution that is logging the message</param>
        /// <param name="serviceTypeName">Name of the service type.</param>
        /// <param name="requestTypeName">Name of the request type.</param>
        /// <param name="duration">Duration of the call in milliseconds.</param>
        /// <param name="error">The error that occurred resulting in this log message being written</param>
        /// <param name="memberName">The member that logged the message within the client code</param>
        /// <param name="fileName">The file name that logged the message within the client code</param>
        /// <param name="lineNumber">The line number in the file that logged the message within the client code</param>
        [NonEvent]
        public void ServiceRequestStop(Guid traceId, string componentName, string serviceTypeName, string requestTypeName, int duration, Exception error = null,
                     [CallerMemberName] string memberName = null, [CallerFilePath] string fileName = null, [CallerLineNumber] int lineNumber = 0)
        {
            this.ServiceRequestEvent(traceId, this.nodeType, this.nodeName, this.serviceUri, this.applicationUri, this.partitionId, this.replicaOrInstanceId, Environment.MachineName, Process.GetCurrentProcess().Id, this.systemName, componentName, $"Service request stopped requestTypeName { requestTypeName }.", duration, error?.Flatten(), memberName, fileName, lineNumber);
        }

        /// <summary>
        /// Service failure request event.
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
        /// <param name="componentName">The component within the clients solution that is logging the message</param>
        /// <param name="serviceTypeName">Name of the service type.</param>
        /// <param name="requestTypeName">Name of the request type.</param>
        /// <param name="exception">Reason for the failure.</param>
        /// <param name="error">The error that occurred resulting in this log message being written</param>
        /// <param name="memberName">The member that logged the message within the client code</param>
        /// <param name="fileName">The file name that logged the message within the client code</param>
        /// <param name="lineNumber">The line number in the file that logged the message within the client code</param>
        [NonEvent]
        public void ServiceRequestFailed(Guid traceId, string componentName, string serviceTypeName, string requestTypeName, string exception, Exception error = null,
                     [CallerMemberName] string memberName = null, [CallerFilePath] string fileName = null, [CallerLineNumber] int lineNumber = 0)
        {
            this.ServiceRequestFailedEvent(traceId, this.nodeType, this.nodeName, this.serviceUri, this.applicationUri, this.partitionId, this.replicaOrInstanceId, Environment.MachineName, Process.GetCurrentProcess().Id, this.systemName, componentName, $"Service request stopped requestTypeName { requestTypeName }.", error?.Flatten(), memberName, fileName, lineNumber);
        }

        /// <summary>
        /// RunAsync invoked event.
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
        /// <param name="componentName">The component within the clients solution that is logging the message</param>
        /// <param name="serviceTypeName">Name of the service type.</param>
        /// <param name="error">The error that occurred resulting in this log message being written</param>
        /// <param name="memberName">The member that logged the message within the client code</param>
        /// <param name="fileName">The file name that logged the message within the client code</param>
        /// <param name="lineNumber">The line number in the file that logged the message within the client code</param>
        [NonEvent]
        public void RunAsyncInvoked(Guid traceId, string componentName, string serviceTypeName, Exception error = null,
                     [CallerMemberName] string memberName = null, [CallerFilePath] string fileName = null, [CallerLineNumber] int lineNumber = 0)
        {
            this.ServiceLifeycleEvent(traceId, this.nodeType, this.nodeName, this.serviceUri, this.applicationUri, this.partitionId, this.replicaOrInstanceId, Environment.MachineName, Process.GetCurrentProcess().Id, this.systemName, componentName, "RunAsync() invoked.", error?.Flatten(), memberName, fileName, lineNumber);
        }

        /// <summary>
        /// Create communication listener event.
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
        /// <param name="componentName">The component within the clients solution that is logging the message</param>
        /// <param name="serviceTypeName">Name of the service type.</param>
        /// <param name="listenAddress">String containing the uri where the communication listener is listening.</param>
        /// <param name="error">The error that occurred resulting in this log message being written</param>
        /// <param name="memberName">The member that logged the message within the client code</param>
        /// <param name="fileName">The file name that logged the message within the client code</param>
        /// <param name="lineNumber">The line number in the file that logged the message within the client code</param>
        [NonEvent]
        public void CreateCommunicationListener(Guid traceId, string componentName, string serviceTypeName, string listenAddress, Exception error = null,
                     [CallerMemberName] string memberName = null, [CallerFilePath] string fileName = null, [CallerLineNumber] int lineNumber = 0)
        {
            this.ServiceLifeycleEvent(traceId, this.nodeType, this.nodeName, this.serviceUri, this.applicationUri, this.partitionId, this.replicaOrInstanceId, Environment.MachineName, Process.GetCurrentProcess().Id, this.systemName, componentName, "Create communication listener.", error?.Flatten(), memberName, fileName, lineNumber);
        }

        /// <summary>
        /// Service partition configuration changed event.
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
        /// <param name="componentName">The component within the clients solution that is logging the message</param>
        /// <param name="serviceTypeName">Name of the service type.</param>
        /// <param name="error">The error that occurred resulting in this log message being written</param>
        /// <param name="memberName">The member that logged the message within the client code</param>
        /// <param name="fileName">The file name that logged the message within the client code</param>
        /// <param name="lineNumber">The line number in the file that logged the message within the client code</param>
        [NonEvent]
        public void ServicePartitionConfigurationChanged(Guid traceId, string componentName, string serviceTypeName, Exception error = null,
                     [CallerMemberName] string memberName = null, [CallerFilePath] string fileName = null, [CallerLineNumber] int lineNumber = 0)
        {
            this.ConfigurationChangeEvent(traceId, this.nodeType, this.nodeName, this.serviceUri, this.applicationUri, this.partitionId, this.replicaOrInstanceId, Environment.MachineName, Process.GetCurrentProcess().Id, this.systemName, componentName, "Configuration changed.", error?.Flatten(), memberName, fileName, lineNumber);
        }

        /// <summary>
        /// Service partition may have experienced data loss.
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
        /// <param name="componentName">The component within the clients solution that is logging the message</param>
        /// <param name="serviceTypeName">Name of the service type.</param>
        /// <param name="error">The error that occurred resulting in this log message being written</param>
        /// <param name="memberName">The member that logged the message within the client code</param>
        /// <param name="fileName">The file name that logged the message within the client code</param>
        /// <param name="lineNumber">The line number in the file that logged the message within the client code</param>
        [NonEvent]
        public void PotentialDataLoss(Guid traceId, string componentName, string serviceTypeName, Exception error = null,
                     [CallerMemberName] string memberName = null, [CallerFilePath] string fileName = null, [CallerLineNumber] int lineNumber = 0)
        {
            this.PartitionDataLossEvent(traceId, this.nodeType, this.nodeName, this.serviceUri, this.applicationUri, this.partitionId, this.replicaOrInstanceId, Environment.MachineName, Process.GetCurrentProcess().Id, this.systemName, componentName, "OnDataLossAsync() invoked.", error?.Flatten(), memberName, fileName, lineNumber);
        }

        /// <summary>
        /// OnOpenAsync invoked event.
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
        /// <param name="componentName">The component within the clients solution that is logging the message</param>
        /// <param name="serviceTypeName">Name of the service type.</param>
        /// <param name="error">The error that occurred resulting in this log message being written</param>
        /// <param name="memberName">The member that logged the message within the client code</param>
        /// <param name="fileName">The file name that logged the message within the client code</param>
        /// <param name="lineNumber">The line number in the file that logged the message within the client code</param>
        [NonEvent]
        public void OpenAsyncInvoked(Guid traceId, string componentName, string serviceTypeName, Exception error = null,
                     [CallerMemberName] string memberName = null, [CallerFilePath] string fileName = null, [CallerLineNumber] int lineNumber = 0)
        {
            this.ServiceLifeycleEvent(traceId, this.nodeType, this.nodeName, this.serviceUri, this.applicationUri, this.partitionId, this.replicaOrInstanceId, Environment.MachineName, Process.GetCurrentProcess().Id, this.systemName, componentName, "OpenAsync() invoked.", error?.Flatten(), memberName, fileName, lineNumber);
        }

        /// <summary>
        /// OnChangeRoleAsync invoked event.
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
        /// <param name="componentName">The component within the clients solution that is logging the message</param>
        /// <param name="serviceTypeName">Name of the service type.</param>
        /// <param name="role">Name of the new role.</param>
        /// <param name="error">The error that occurred resulting in this log message being written</param>
        /// <param name="memberName">The member that logged the message within the client code</param>
        /// <param name="fileName">The file name that logged the message within the client code</param>
        /// <param name="lineNumber">The line number in the file that logged the message within the client code</param>
        [NonEvent]
        public void ChangeRoleAsyncInvoked(Guid traceId, string componentName, string serviceTypeName, string role, Exception error = null,
                     [CallerMemberName] string memberName = null, [CallerFilePath] string fileName = null, [CallerLineNumber] int lineNumber = 0)
        {
            this.ServiceLifeycleEvent(traceId, this.nodeType, this.nodeName, this.serviceUri, this.applicationUri, this.partitionId, this.replicaOrInstanceId, Environment.MachineName, Process.GetCurrentProcess().Id, this.systemName, componentName, $"ChangeRoleAsync() invoked role: { role }.", error?.Flatten(), memberName, fileName, lineNumber);
        }

        /// <summary>
        /// OnCloseAsync invoked event.
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
        /// <param name="componentName">The component within the clients solution that is logging the message</param>
        /// <param name="serviceTypeName">Name of the service type.</param>
        /// <param name="error">The error that occurred resulting in this log message being written</param>
        /// <param name="memberName">The member that logged the message within the client code</param>
        /// <param name="fileName">The file name that logged the message within the client code</param>
        /// <param name="lineNumber">The line number in the file that logged the message within the client code</param>
        [NonEvent]
        public void CloseAsyncInvoked(Guid traceId, string componentName, string serviceTypeName, Exception error = null,
                     [CallerMemberName] string memberName = null, [CallerFilePath] string fileName = null, [CallerLineNumber] int lineNumber = 0)
        {
            this.ServiceLifeycleEvent(traceId, this.nodeType, this.nodeName, this.serviceUri, this.applicationUri, this.partitionId, this.replicaOrInstanceId, Environment.MachineName, Process.GetCurrentProcess().Id, this.systemName, componentName, "CloseAsync() invoked.", error?.Flatten(), memberName, fileName, lineNumber);
        }

        /// <summary>
        /// OnAbort invoked event.
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
        /// <param name="componentName">The component within the clients solution that is logging the message</param>
        /// <param name="serviceTypeName">Name of the service type.</param>
        /// <param name="error">The error that occurred resulting in this log message being written</param>
        /// <param name="memberName">The member that logged the message within the client code</param>
        /// <param name="fileName">The file name that logged the message within the client code</param>
        /// <param name="lineNumber">The line number in the file that logged the message within the client code</param>
        [NonEvent]
        public void AbortInvoked(Guid traceId, string componentName, string serviceTypeName, Exception error = null,
                     [CallerMemberName] string memberName = null, [CallerFilePath] string fileName = null, [CallerLineNumber] int lineNumber = 0)
        {
            this.ServiceLifeycleEvent(traceId, this.nodeType, this.nodeName, this.serviceUri, this.applicationUri, this.partitionId, this.replicaOrInstanceId, Environment.MachineName, Process.GetCurrentProcess().Id, this.systemName, componentName, "Abort() invoked.", error?.Flatten(), memberName, fileName, lineNumber);
        }
        #endregion
        #region Support methods
        /// <summary>
        /// Determines the size in number of bytes of a string for unmanaged code
        /// </summary>
        /// <param name="value">The string that the size is being determined for</param>
        /// <returns>The size of the string in bytes</returns>
        int SizeInBytes(string value)
        {
            // ReSharper disable once MergeConditionalExpression
            return value == null ? 0 : (value.Length + 1) * sizeof(char);
        }

        /// <summary>
        /// Builds the message text
        /// </summary>
        /// <param name="message">The message to write and if a string type with parameters present it will write it as the format string</param>
        /// <param name="parameters">The ordered list of parameters to be inserted into the placeholders of the format string</param>
        /// <returns>The message string based on the parameters and message</returns>
        static string GetMessageString<T>(T message, object[] parameters)
        {
            string messageString = message?.ToString();

            if (messageString != null && parameters != null && parameters.Length > 0)
            {
                messageString = string.Format(messageString, parameters);
            }

            return messageString;
        }

        #endregion
    }
}
