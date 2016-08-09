namespace ProtocolGateway.Host.Fabric.FabricShared.Logging
{
    #region Using Clauses
    using System;
    using System.Text;
    #endregion

    /// <summary>
    /// Extension class used for flattening exceptions
    /// </summary>
    public static class ExceptionExtensions
    {
        #region Public Methods
        /// <summary>
        /// Flattens the exceptions error message including all inner exceptions.
        /// </summary>
        /// <param name="exception">The exception to flatten.</param>
        /// <returns>A string representing the exceptions error message concatenated with the inner exception messages.</returns>
        public static string Flatten(this Exception exception)
        {
            string flatException = string.Empty;

            if (exception != null)
            {
                var aggregateException = exception as AggregateException;

                flatException = aggregateException == null ? FlattenException(exception) : FlattenAggregateException(aggregateException);
            }

            return flatException;
        }
        #endregion
        #region Private Methods
        /// <summary>
        /// Flattens a standard exception object.
        /// </summary>
        /// <param name="exception">The exception to be flattened.</param>
        /// <returns>A string that concatenates all inner exception messages as well.</returns>
        static string FlattenException(Exception exception)
        {
            var builder = new StringBuilder(exception.Message);

            if (exception.InnerException != null)
            {
                var aggregateException = exception.InnerException as AggregateException;

                builder.AppendLine(aggregateException == null
                    ? FlattenException(exception.InnerException)
                    : FlattenAggregateException(aggregateException));
            }

            return builder.ToString();
        }

        /// <summary>
        /// Flattens an aggregate exception object.
        /// </summary>
        /// <param name="exception">An aggregate exception object to be flattened.</param>
        /// <returns>A string that concatenates all inner exception messages as well.</returns>
        static string FlattenAggregateException(AggregateException exception)
        {
            var builder = new StringBuilder();

            if (exception.InnerExceptions != null)
            {
                builder.AppendLine(exception.Message);

                foreach (Exception innerException in exception.InnerExceptions)
                {
                    var innerAggregateException = innerException as AggregateException;

                    builder.AppendLine(innerAggregateException != null
                        ? FlattenAggregateException(innerAggregateException)
                        : FlattenException(innerException));
                }
            }

            return builder.ToString();
        }
        #endregion
    }
}
