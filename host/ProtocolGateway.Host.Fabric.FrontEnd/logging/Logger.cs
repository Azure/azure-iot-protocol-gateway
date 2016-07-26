namespace ProtocolGateway.Host.Fabric.FrontEnd.Logging
{
    #region Using Clauses
    using System;
    using System.Diagnostics.Tracing;
    using ProtocolGateway.Host.Fabric.FabricShared.Logging;
    #endregion

    /// <summary>
    /// An event source used initiating ETW events
    /// </summary>
    [EventSource(Name = "Microsoft-ProtocolGateway-Application")]
    public sealed class Logger : LoggerBase
    {
        #region Constructors

        /// <summary>
        /// Initializes a new instance of the type
        /// </summary>
        /// <param name="systemName">The name of the system the logger is recording data for</param>
        public Logger(string systemName) : base(systemName)
        {
        }

        #endregion
        #region Event Decorated Writers
        /// <summary>
        /// The method describing the verbose event for the ETW Manifest
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
        /// <param name="computerName">The name of the computer the event is being written from</param>
        /// <param name="processId">The process id that the event originated from</param>
        /// <param name="systemName">The solution or service name that is logging the message</param>
        /// <param name="componentName">The component within the clients solution that is logging the message</param>
        /// <param name="message">The message to log</param>
        /// <param name="errorMessage">The string representation of the exception that ocurred</param>
        /// <param name="memberName">The member that logged the message within the client code</param>
        /// <param name="fileName">The file name that logged the message within the client code</param>
        /// <param name="lineNumber">The line number in the file that logged the message within the client code</param>
        [Event(1, Keywords = Keywords.StandardMessage, Level = EventLevel.Verbose, Message = "{5}")]
        protected override void VerboseStandardMessage(Guid traceId, string computerName, int processId,
            string systemName, string componentName, string message, string errorMessage, string memberName,
            string fileName, int lineNumber)
        {
            if (this.IsEnabled(EventLevel.Verbose, Keywords.StandardMessage))
            {
                this.WriteStandardLogEvent(1, traceId, computerName, processId, systemName, componentName, message, errorMessage, memberName, fileName, lineNumber);
            }
        }

        /// <summary>
        /// The method describing the verbose event for the ETW Manifest
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
        /// <param name="computerName">The name of the computer the event is being written from</param>
        /// <param name="processId">The process id that the event originated from</param>
        /// <param name="systemName">The solution or service name that is logging the message</param>
        /// <param name="componentName">The component within the clients solution that is logging the message</param>
        /// <param name="message">The message to log</param>
        /// <param name="errorMessage">The string representation of the exception that ocurred</param>
        /// <param name="memberName">The member that logged the message within the client code</param>
        /// <param name="fileName">The file name that logged the message within the client code</param>
        /// <param name="lineNumber">The line number in the file that logged the message within the client code</param>
        [Event(2, Keywords = Keywords.StandardMessage, Level = EventLevel.Informational, Message = "{5}")]
        protected override void InformationalStandardMessage(Guid traceId, string computerName, int processId,
            string systemName, string componentName, string message, string errorMessage, string memberName,
            string fileName, int lineNumber)
        {
            if (this.IsEnabled(EventLevel.Informational, Keywords.StandardMessage))
            {
                this.WriteStandardLogEvent(2, traceId, computerName, processId, systemName, componentName, message,
                    errorMessage,
                    memberName, fileName, lineNumber);
            }
        }

        /// <summary>
        /// The method describing the verbose event for the ETW Manifest
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
        /// <param name="computerName">The name of the computer the event is being written from</param>
        /// <param name="processId">The process id that the event originated from</param>
        /// <param name="systemName">The solution or service name that is logging the message</param>
        /// <param name="componentName">The component within the clients solution that is logging the message</param>
        /// <param name="message">The message to log</param>
        /// <param name="errorMessage">The string representation of the exception that ocurred</param>
        /// <param name="memberName">The member that logged the message within the client code</param>
        /// <param name="fileName">The file name that logged the message within the client code</param>
        /// <param name="lineNumber">The line number in the file that logged the message within the client code</param>
        [Event(3, Keywords = Keywords.StandardMessage, Level = EventLevel.Warning, Message = "{5}")]
        protected override void WarningStandardMessage(Guid traceId, string computerName, int processId,
            string systemName, string componentName, string message, string errorMessage, string memberName,
            string fileName, int lineNumber)
        {
            if (this.IsEnabled(EventLevel.Warning, Keywords.StandardMessage))
            {
                this.WriteStandardLogEvent(3, traceId, computerName, processId, systemName, componentName, message,
                    errorMessage,
                    memberName, fileName, lineNumber);
            }
        }

        /// <summary>
        /// The method describing the verbose event for the ETW Manifest
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
        /// <param name="computerName">The name of the computer the event is being written from</param>
        /// <param name="processId">The process id that the event originated from</param>
        /// <param name="systemName">The solution or service name that is logging the message</param>
        /// <param name="componentName">The component within the clients solution that is logging the message</param>
        /// <param name="message">The message to log</param>
        /// <param name="errorMessage">The string representation of the exception that ocurred</param>
        /// <param name="memberName">The member that logged the message within the client code</param>
        /// <param name="fileName">The file name that logged the message within the client code</param>
        /// <param name="lineNumber">The line number in the file that logged the message within the client code</param>
        [Event(4, Keywords = Keywords.StandardMessage, Level = EventLevel.Error, Message = "{5}")]
        protected override void ErrorStandardMessage(Guid traceId, string computerName, int processId, string systemName,
            string componentName, string message, string errorMessage, string memberName, string fileName,
            int lineNumber)
        {
            if (this.IsEnabled(EventLevel.Error, Keywords.StandardMessage))
            {
                this.WriteStandardLogEvent(4, traceId, computerName, processId, systemName, componentName, message,
                    errorMessage,
                    memberName, fileName, lineNumber);
            }
        }

        /// <summary>
        /// The method describing the verbose event for the ETW Manifest
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
        /// <param name="computerName">The name of the computer the event is being written from</param>
        /// <param name="processId">The process id that the event originated from</param>
        /// <param name="systemName">The solution or service name that is logging the message</param>
        /// <param name="componentName">The component within the clients solution that is logging the message</param>
        /// <param name="message">The message to log</param>
        /// <param name="errorMessage">The string representation of the exception that ocurred</param>
        /// <param name="memberName">The member that logged the message within the client code</param>
        /// <param name="fileName">The file name that logged the message within the client code</param>
        /// <param name="lineNumber">The line number in the file that logged the message within the client code</param>
        [Event(5, Keywords = Keywords.StandardMessage, Level = EventLevel.Critical, Message = "{5}")]
        protected override void CriticalStandardMessage(Guid traceId, string computerName, int processId,
            string systemName, string componentName, string message, string errorMessage, string memberName,
            string fileName, int lineNumber)
        {
            if (this.IsEnabled(EventLevel.Critical, Keywords.StandardMessage))
            {
                this.WriteStandardLogEvent(5, traceId, computerName, processId, systemName, componentName, message,
                    errorMessage,
                    memberName, fileName, lineNumber);
            }
        }

        /// <summary>
        /// The method describing the verbose event for the ETW Manifest
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
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
        [Event(6, Keywords = Keywords.TimingMessage, Level = EventLevel.Verbose, Message = "{5}")]
        protected override void VerboseTimingMessage(Guid traceId, string computerName, int processId, string systemName,
            string componentName, string message, double timing, string errorMessage, string memberName, string fileName,
            int lineNumber)
        {
            if (this.IsEnabled(EventLevel.Verbose, Keywords.TimingMessage))
            {
                this.WriteTimingLogEvent(6, traceId, computerName, processId, systemName, componentName, message, timing,
                    errorMessage, memberName, fileName, lineNumber);
            }
        }

        /// <summary>
        /// The method describing the verbose event for the ETW Manifest
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
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
        [Event(7, Keywords = Keywords.TimingMessage, Level = EventLevel.Informational, Message = "{5}")]
        protected override void InformationalTimingMessage(Guid traceId, string computerName, int processId,
            string systemName, string componentName, string message, double timing, string errorMessage,
            string memberName, string fileName, int lineNumber)
        {
            if (this.IsEnabled(EventLevel.Informational, Keywords.TimingMessage))
            {
                this.WriteTimingLogEvent(7, traceId, computerName, processId, systemName, componentName, message, timing,
                    errorMessage, memberName, fileName, lineNumber);
            }
        }

        /// <summary>
        /// The method describing the verbose event for the ETW Manifest
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
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
        [Event(8, Keywords = Keywords.TimingMessage, Level = EventLevel.Warning, Message = "{5}")]
        protected override void WarningTimingMessage(Guid traceId, string computerName, int processId, string systemName,
            string componentName, string message, double timing, string errorMessage, string memberName, string fileName,
            int lineNumber)
        {
            if (this.IsEnabled(EventLevel.Warning, Keywords.TimingMessage))
            {
                this.WriteTimingLogEvent(8, traceId, computerName, processId, systemName, componentName, message, timing,
                    errorMessage, memberName, fileName, lineNumber);
            }
        }

        /// <summary>
        /// The method describing the verbose event for the ETW Manifest
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
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
        [Event(9, Keywords = Keywords.TimingMessage, Level = EventLevel.Error, Message = "{5}")]
        protected override void ErrorTimingMessage(Guid traceId, string computerName, int processId, string systemName,
            string componentName, string message, double timing, string errorMessage, string memberName, string fileName,
            int lineNumber)
        {
            if (this.IsEnabled(EventLevel.Error, Keywords.TimingMessage))
            {
                this.WriteTimingLogEvent(9, traceId, computerName, processId, systemName, componentName, message, timing,
                    errorMessage, memberName, fileName, lineNumber);
            }
        }

        /// <summary>
        /// The method describing the verbose event for the ETW Manifest
        /// </summary>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
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
        [Event(10, Keywords = Keywords.TimingMessage, Level = EventLevel.Critical, Message = "{5}")]
        protected override void CriticalTimingMessage(Guid traceId, string computerName, int processId,
            string systemName, string componentName, string message, double timing, string errorMessage,
            string memberName, string fileName, int lineNumber)
        {
            if (this.IsEnabled(EventLevel.Critical, Keywords.TimingMessage))
            {
                this.WriteTimingLogEvent(10, traceId, computerName, processId, systemName, componentName, message, timing,
                    errorMessage, memberName, fileName, lineNumber);
            }
        }

        #endregion
        #region Keywords
        // Event keywords can be used to categorize events. 
        // Each keyword is a bit flag. A single event can be associated with multiple keywords (via EventAttribute.Keywords property).
        // Keywords must be defined as a public class named 'Keywords' inside EventSource that uses them.
        public sealed class Keywords
        {
            /// <summary>
            /// A standard log event
            /// </summary>
            public const EventKeywords StandardMessage = (EventKeywords)0x1L;

            /// <summary>
            /// A timing log event that requires service fabric metadata for context
            /// </summary>
            public const EventKeywords TimingMessage = (EventKeywords)0x2L;
        }
        #endregion
    }
}
