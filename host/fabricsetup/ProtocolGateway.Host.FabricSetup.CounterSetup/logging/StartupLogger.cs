namespace ProtocolGateway.Host.FabricSetup.CounterSetup.logging
{
    #region Using Clauses
    using System;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Diagnostics.Tracing;
    using System.Security;
    using System.Threading.Tasks;
    using Fabric.FabricShared.Logging;
    #endregion

    /// <summary>
    /// An event source used initiating ETW events
    /// </summary>
    [EventSource(Name = "ProtocolGateway-Logger-Startup")]
    public sealed class StartupLogger : EventSource, ILogger
    {
        #region Variables
        /// <summary>
        /// The system that the logger is recording information for
        /// </summary>
        readonly string defaultSystemName;
        #endregion
        #region Constructors
        /// <summary>
        /// A static constructor only implemented to work around a problem where the ETW activities are not initialized immediately.  This is expected to be fixed in .NET Framework 4.6.2 when
        /// it can be removed
        /// </summary>
        static StartupLogger()
        {
            Task.Run(() => { }).Wait();
        }

        /// <summary>
        /// An private constructor to limit creation to using the singleton
        /// </summary>
        /// <param name="systemName">The name of the system the logger is recording data for</param>
        public StartupLogger(string systemName)
        {
            this.defaultSystemName = systemName;
        }
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
            if (this.IsEnabled(EventLevel.Critical, Keywords.StandardMessage))
            {
                this.CriticalStandardMessage(traceId, Environment.MachineName, Process.GetCurrentProcess().Id, this.defaultSystemName, componentName, GetMessageString(message, parameters), error?.Flatten(), memberName, fileName, lineNumber);
            }
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
            if (this.IsEnabled(EventLevel.Critical, Keywords.TimingMessage))
            {
                this.CriticalTimingMessage(traceId, Environment.MachineName, Process.GetCurrentProcess().Id, this.defaultSystemName, componentName, GetMessageString(message, parameters), timing, error?.Flatten(), memberName, fileName, lineNumber);
            }
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
            if (this.IsEnabled(EventLevel.Error, Keywords.StandardMessage))
            {
                this.ErrorStandardMessage(traceId, Environment.MachineName, Process.GetCurrentProcess().Id, this.defaultSystemName, componentName, GetMessageString(message, parameters), error?.Flatten(), memberName, fileName, lineNumber);
            }
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
            if (this.IsEnabled(EventLevel.Error, Keywords.TimingMessage))
            {
                this.ErrorTimingMessage(traceId, Environment.MachineName, Process.GetCurrentProcess().Id, this.defaultSystemName, componentName, GetMessageString(message, parameters), timing, error?.Flatten(), memberName, fileName, lineNumber);
            }
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
            if (this.IsEnabled(EventLevel.Informational, Keywords.StandardMessage))
            {
                this.InformationalStandardMessage(traceId, Environment.MachineName, Process.GetCurrentProcess().Id, this.defaultSystemName, componentName, GetMessageString(message, parameters), error?.Flatten(), memberName, fileName, lineNumber);
            }
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
            if (this.IsEnabled(EventLevel.Informational, Keywords.TimingMessage))
            {
                this.InformationalTimingMessage(traceId, Environment.MachineName, Process.GetCurrentProcess().Id, this.defaultSystemName, componentName, GetMessageString(message, parameters), timing, error?.Flatten(), memberName, fileName, lineNumber);
            }
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
            if (this.IsEnabled(EventLevel.Verbose, Keywords.StandardMessage))
            {
                this.VerboseStandardMessage(traceId, Environment.MachineName, Process.GetCurrentProcess().Id, this.defaultSystemName, componentName, GetMessageString(message, parameters), error?.Flatten(), memberName, fileName, lineNumber);
            }
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
            if (this.IsEnabled(EventLevel.Verbose, Keywords.TimingMessage))
            {
                this.VerboseTimingMessage(traceId, Environment.MachineName, Process.GetCurrentProcess().Id, this.defaultSystemName, componentName, GetMessageString(message, parameters), timing, error?.Flatten(), memberName, fileName, lineNumber);
            }
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
            if (this.IsEnabled(EventLevel.Warning, Keywords.StandardMessage))
            {
                this.WarningStandardMessage(traceId, Environment.MachineName, Process.GetCurrentProcess().Id, this.defaultSystemName, componentName, GetMessageString(message, parameters), error?.Flatten(), memberName, fileName, lineNumber);
            }
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
            if (this.IsEnabled(EventLevel.Warning, Keywords.TimingMessage))
            {
                this.WarningTimingMessage(traceId, Environment.MachineName, Process.GetCurrentProcess().Id, this.defaultSystemName, componentName, GetMessageString(message, parameters), timing, error?.Flatten(), memberName, fileName, lineNumber);
            }
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
        void VerboseStandardMessage(Guid traceId, string computerName, int processId, string systemName, string componentName, string message, string errorMessage, string memberName, string fileName, int lineNumber)
        {
            this.WriteStandardLogEvent(1, traceId, computerName, processId, systemName, componentName, message, errorMessage, memberName, fileName, lineNumber);
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
        void InformationalStandardMessage(Guid traceId, string computerName, int processId, string systemName, string componentName, string message, string errorMessage, string memberName, string fileName, int lineNumber)
        {
            this.WriteStandardLogEvent(2, traceId, computerName, processId, systemName, componentName, message, errorMessage, memberName, fileName, lineNumber);
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
        void WarningStandardMessage(Guid traceId, string computerName, int processId, string systemName, string componentName, string message, string errorMessage, string memberName, string fileName, int lineNumber)
        {
            this.WriteStandardLogEvent(3, traceId, computerName, processId, systemName, componentName, message, errorMessage, memberName, fileName, lineNumber);
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
        void ErrorStandardMessage(Guid traceId, string computerName, int processId, string systemName, string componentName, string message, string errorMessage, string memberName, string fileName, int lineNumber)
        {
            this.WriteStandardLogEvent(4, traceId, computerName, processId, systemName, componentName, message, errorMessage, memberName, fileName, lineNumber);
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
        void CriticalStandardMessage(Guid traceId, string computerName, int processId, string systemName, string componentName, string message, string errorMessage, string memberName, string fileName, int lineNumber)
        {
            this.WriteStandardLogEvent(5, traceId, computerName, processId, systemName, componentName, message, errorMessage, memberName, fileName, lineNumber);
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
        [Event(6, Keywords = Keywords.StandardMessage, Level = EventLevel.Verbose, Message = "{5}")]
        void VerboseTimingMessage(Guid traceId, string computerName, int processId, string systemName, string componentName, string message, double timing, string errorMessage, string memberName, string fileName, int lineNumber)
        {
            this.WriteTimingLogEvent(6, traceId, computerName, processId, systemName, componentName, message, timing, errorMessage, memberName, fileName, lineNumber);
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
        [Event(7, Keywords = Keywords.StandardMessage, Level = EventLevel.Informational, Message = "{5}")]
        void InformationalTimingMessage(Guid traceId, string computerName, int processId, string systemName, string componentName, string message, double timing, string errorMessage, string memberName, string fileName, int lineNumber)
        {
            this.WriteTimingLogEvent(7, traceId, computerName, processId, systemName, componentName, message, timing, errorMessage, memberName, fileName, lineNumber);
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
        [Event(8, Keywords = Keywords.StandardMessage, Level = EventLevel.Warning, Message = "{5}")]
        void WarningTimingMessage(Guid traceId, string computerName, int processId, string systemName, string componentName, string message, double timing, string errorMessage, string memberName, string fileName, int lineNumber)
        {
            this.WriteTimingLogEvent(8, traceId, computerName, processId, systemName, componentName, message, timing, errorMessage, memberName, fileName, lineNumber);
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
        [Event(9, Keywords = Keywords.StandardMessage, Level = EventLevel.Error, Message = "{5}")]
        void ErrorTimingMessage(Guid traceId, string computerName, int processId, string systemName, string componentName, string message, double timing, string errorMessage, string memberName, string fileName, int lineNumber)
        {
            this.WriteTimingLogEvent(9, traceId, computerName, processId, systemName, componentName, message, timing, errorMessage, memberName, fileName, lineNumber);
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
        [Event(10, Keywords = Keywords.StandardMessage, Level = EventLevel.Critical, Message = "{5}")]
        void CriticalTimingMessage(Guid traceId, string computerName, int processId, string systemName, string componentName, string message, double timing, string errorMessage, string memberName, string fileName, int lineNumber)
        {
            this.WriteTimingLogEvent(10, traceId, computerName, processId, systemName, componentName, message, timing, errorMessage, memberName, fileName, lineNumber);
        }
        #endregion
        #region Private Event Writers
        /// <summary>
        /// Performs an unsafe write of the log event for performance
        /// </summary>
        /// <param name="eventId">The id of the event being logged</param>
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
        [NonEvent]
        [SuppressUnmanagedCodeSecurity]
        unsafe void WriteStandardLogEvent(int eventId, Guid traceId, string computerName, int processId, string systemName, string componentName, string message, string errorMessage, string memberName, string fileName, int lineNumber)
        {
            const int ArgumentCount = 10;
            const string NullString = "";

            if (systemName == null) systemName = NullString;
            if (componentName == null) componentName = NullString;
            if (message == null) message = NullString;
            if (errorMessage == null) errorMessage = NullString;
            if (fileName == null) fileName = NullString;
            if (memberName == null) memberName = NullString;

            fixed (char* pMessage = message, pMemberName = memberName, pFileName = fileName, pSystemName = systemName, pComponentName = componentName, pErrorMessage = errorMessage,
                pComputerName = computerName)
            {
                EventData* eventData = stackalloc EventData[ArgumentCount];

                eventData[0] = new EventData { DataPointer = (IntPtr)(&traceId), Size = sizeof(Guid) };
                eventData[1] = new EventData { DataPointer = (IntPtr)pComputerName, Size = this.SizeInBytes(computerName) };
                eventData[2] = new EventData { DataPointer = (IntPtr)(&processId), Size = sizeof(int) };
                eventData[3] = new EventData { DataPointer = (IntPtr)pSystemName, Size = this.SizeInBytes(systemName) };


                eventData[4] = new EventData { DataPointer = (IntPtr)pComponentName, Size = this.SizeInBytes(componentName) };
                eventData[5] = new EventData { DataPointer = (IntPtr)pMessage, Size = this.SizeInBytes(message) };
                eventData[6] = new EventData { DataPointer = (IntPtr)pErrorMessage, Size = this.SizeInBytes(errorMessage) };
                eventData[7] = new EventData { DataPointer = (IntPtr)pMemberName, Size = this.SizeInBytes(memberName) };
                eventData[8] = new EventData { DataPointer = (IntPtr)pFileName, Size = this.SizeInBytes(fileName) };
                eventData[9] = new EventData { DataPointer = (IntPtr)(&lineNumber), Size = sizeof(int) };

                this.WriteEventCore(eventId, ArgumentCount, eventData);
            }
        }

        /// <summary>
        /// Performs an unsafe write of the log event for performance
        /// </summary>
        /// <param name="eventId">The id of the event being logged</param>
        /// <param name="traceId">A unique identifier used to correlated related log messages</param>
        /// <param name="computerName">The name of the computer the event is being written from</param>
        /// <param name="processId">The process id that the event originated from</param>
        /// <param name="systemName">The solution or service name that is logging the message</param>
        /// <param name="componentName">The component within the clients solution that is logging the message</param>
        /// <param name="message">The message to log</param>
        /// <param name="timing">The time typically in milliseconds that is associated with the log entry</param>
        /// <param name="errorMessage">The string representation of the exception that ocurred</param>
        /// <param name="memberName">The member that logged the message within the client code</param>
        /// <param name="fileName">The file name that logged the message within the client code</param>
        /// <param name="lineNumber">The line number in the file that logged the message within the client code</param>
        [NonEvent]
        [SuppressUnmanagedCodeSecurity]
        unsafe void WriteTimingLogEvent(int eventId, Guid traceId, string computerName, int processId, string systemName, string componentName, string message, double timing, string errorMessage, string memberName, string fileName, int lineNumber)
        {
            const int ArgumentCount = 11;
            const string NullString = "";

            if (systemName == null) systemName = NullString;
            if (componentName == null) componentName = NullString;
            if (message == null) message = NullString;
            if (errorMessage == null) errorMessage = NullString;
            if (fileName == null) fileName = NullString;
            if (memberName == null) memberName = NullString;

            fixed (char* pMessage = message, pMemberName = memberName, pFileName = fileName, pSystemName = systemName, pComponentName = componentName, pErrorMessage = errorMessage,
                pComputerName = computerName)
            {
                EventData* eventData = stackalloc EventData[ArgumentCount];

                eventData[0] = new EventData { DataPointer = (IntPtr)(&traceId), Size = sizeof(Guid) };
                eventData[1] = new EventData { DataPointer = (IntPtr)pComputerName, Size = this.SizeInBytes(computerName) };
                eventData[2] = new EventData { DataPointer = (IntPtr)(&processId), Size = sizeof(int) };
                eventData[3] = new EventData { DataPointer = (IntPtr)pSystemName, Size = this.SizeInBytes(systemName) };
                eventData[4] = new EventData { DataPointer = (IntPtr)pComponentName, Size = this.SizeInBytes(componentName) };
                eventData[5] = new EventData { DataPointer = (IntPtr)pMessage, Size = this.SizeInBytes(message) };
                eventData[6] = new EventData { DataPointer = (IntPtr)(&timing), Size = sizeof(double) };
                eventData[7] = new EventData { DataPointer = (IntPtr)pErrorMessage, Size = this.SizeInBytes(errorMessage) };
                eventData[8] = new EventData { DataPointer = (IntPtr)pMemberName, Size = this.SizeInBytes(memberName) };
                eventData[9] = new EventData { DataPointer = (IntPtr)pFileName, Size = this.SizeInBytes(fileName) };
                eventData[10] = new EventData { DataPointer = (IntPtr)(&lineNumber), Size = sizeof(int) };

                this.WriteEventCore(eventId, ArgumentCount, eventData);
            }
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
        #region Keywords
        // Event keywords can be used to categorize events. 
        // Each keyword is a bit flag. A single event can be associated with multiple keywords (via EventAttribute.Keywords property).
        // Keywords must be defined as a public class named 'Keywords' inside EventSource that uses them.
        public static class Keywords
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
