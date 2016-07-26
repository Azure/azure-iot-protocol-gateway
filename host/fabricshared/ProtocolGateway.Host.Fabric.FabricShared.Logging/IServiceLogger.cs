namespace ProtocolGateway.Host.Fabric.FabricShared.Logging
{
    #region Using Clauses
    using System;
    using System.Runtime.CompilerServices;
    #endregion

    /// <summary>
    /// A logger with additional methods used to write events associated with Service Fabric 
    /// </summary>
    public interface IServiceLogger : ILogger
    {
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
        void ServiceInstanceConstructed(Guid traceId, string componentName, string serviceTypeName, Exception error = null,
                     [CallerMemberName] string memberName = null, [CallerFilePath] string fileName = null, [CallerLineNumber] int lineNumber = 0);


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
        void ServiceRequestStart(Guid traceId, string componentName, string serviceTypeName, string requestTypeName, Exception error = null,
                     [CallerMemberName] string memberName = null, [CallerFilePath] string fileName = null, [CallerLineNumber] int lineNumber = 0);

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
        void ServiceRequestStop(Guid traceId, string componentName, string serviceTypeName, string requestTypeName, int duration, Exception error = null,
                     [CallerMemberName] string memberName = null, [CallerFilePath] string fileName = null, [CallerLineNumber] int lineNumber = 0);

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
        void ServiceRequestFailed(Guid traceId, string componentName, string serviceTypeName, string requestTypeName, string exception, Exception error = null,
                     [CallerMemberName] string memberName = null, [CallerFilePath] string fileName = null, [CallerLineNumber] int lineNumber = 0);

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
        void RunAsyncInvoked(Guid traceId, string componentName, string serviceTypeName, Exception error = null,
                     [CallerMemberName] string memberName = null, [CallerFilePath] string fileName = null, [CallerLineNumber] int lineNumber = 0);

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
        void CreateCommunicationListener(Guid traceId, string componentName, string serviceTypeName, string listenAddress, Exception error = null,
                     [CallerMemberName] string memberName = null, [CallerFilePath] string fileName = null, [CallerLineNumber] int lineNumber = 0);

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
        void ServicePartitionConfigurationChanged(Guid traceId, string componentName, string serviceTypeName, Exception error = null,
                     [CallerMemberName] string memberName = null, [CallerFilePath] string fileName = null, [CallerLineNumber] int lineNumber = 0);

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
        void PotentialDataLoss(Guid traceId, string componentName, string serviceTypeName, Exception error = null,
                     [CallerMemberName] string memberName = null, [CallerFilePath] string fileName = null, [CallerLineNumber] int lineNumber = 0);

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
        void OpenAsyncInvoked(Guid traceId, string componentName, string serviceTypeName, Exception error = null,
                     [CallerMemberName] string memberName = null, [CallerFilePath] string fileName = null, [CallerLineNumber] int lineNumber = 0);

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
        void ChangeRoleAsyncInvoked(Guid traceId, string componentName, string serviceTypeName, string role, Exception error = null,
                     [CallerMemberName] string memberName = null, [CallerFilePath] string fileName = null, [CallerLineNumber] int lineNumber = 0);

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
        void CloseAsyncInvoked(Guid traceId, string componentName, string serviceTypeName, Exception error = null,
                     [CallerMemberName] string memberName = null, [CallerFilePath] string fileName = null, [CallerLineNumber] int lineNumber = 0);

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
        void AbortInvoked(Guid traceId, string componentName, string serviceTypeName, Exception error = null,
                     [CallerMemberName] string memberName = null, [CallerFilePath] string fileName = null, [CallerLineNumber] int lineNumber = 0);
    }
}
