namespace ProtocolGateway.Host.Fabric.FabricShared.Logging
{
    #region Using Clauses
    using System;
    using System.Runtime.CompilerServices;
    #endregion

    /// <summary>
    /// A logging interface abstracting the logger from the implementation
    /// </summary>
    public interface ILogger
    {
        #region Logging Methods
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
        void Verbose<T>(Guid traceId, string componentName, T message, object[] parameters = null, Exception error = null,
                     [CallerMemberName] string memberName = null, [CallerFilePath] string fileName = null, [CallerLineNumber] int lineNumber = 0);

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
        void Informational<T>(Guid traceId, string componentName, T message, object[] parameters = null, Exception error = null,
                     [CallerMemberName] string memberName = null, [CallerFilePath] string fileName = null, [CallerLineNumber] int lineNumber = 0);

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
        void Warning<T>(Guid traceId, string componentName, T message, object[] parameters = null, Exception error = null,
                     [CallerMemberName] string memberName = null, [CallerFilePath] string fileName = null, [CallerLineNumber] int lineNumber = 0);

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
        void Error<T>(Guid traceId, string componentName, T message, object[] parameters = null, Exception error = null,
                     [CallerMemberName] string memberName = null, [CallerFilePath] string fileName = null, [CallerLineNumber] int lineNumber = 0);

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
        void Critical<T>(Guid traceId, string componentName, T message, object[] parameters = null, Exception error = null,
                     [CallerMemberName] string memberName = null, [CallerFilePath] string fileName = null, [CallerLineNumber] int lineNumber = 0);



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
        void Verbose<T>(Guid traceId, string componentName, double timing, T message, object[] parameters = null, Exception error = null,
                     [CallerMemberName] string memberName = null, [CallerFilePath] string fileName = null, [CallerLineNumber] int lineNumber = 0);

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
        void Informational<T>(Guid traceId, string componentName, double timing, T message, object[] parameters = null, Exception error = null,
                     [CallerMemberName] string memberName = null, [CallerFilePath] string fileName = null, [CallerLineNumber] int lineNumber = 0);

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
        void Warning<T>(Guid traceId, string componentName, double timing, T message, object[] parameters = null, Exception error = null,
                     [CallerMemberName] string memberName = null, [CallerFilePath] string fileName = null, [CallerLineNumber] int lineNumber = 0);

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
        void Error<T>(Guid traceId, string componentName, double timing, T message, object[] parameters = null, Exception error = null,
                     [CallerMemberName] string memberName = null, [CallerFilePath] string fileName = null, [CallerLineNumber] int lineNumber = 0);

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
        void Critical<T>(Guid traceId, string componentName, double timing, T message, object[] parameters = null, Exception error = null,
                     [CallerMemberName] string memberName = null, [CallerFilePath] string fileName = null, [CallerLineNumber] int lineNumber = 0);
        #endregion
    }
}
