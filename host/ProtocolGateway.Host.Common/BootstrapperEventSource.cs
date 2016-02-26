// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ProtocolGateway.Host.Common
{
    using System;
    using System.Diagnostics.Tracing;

    [EventSource(
        Name = "IoT-ProtocolGateway-Bootstrapper",
        Guid = "e29735c9-6796-4228-ac96-9db40faB697a")]
    public class BootstrapperEventSource : EventSource
    {
        const int VerboseEventId = 1;
        const int InfoEventId = 2;
        const int WarningEventId = 3;
        const int ErrorEventId = 4;

        public static readonly BootstrapperEventSource Log = new BootstrapperEventSource();

        BootstrapperEventSource()
        {
        }

        public bool IsVerboseEnabled
        {
            get { return this.IsEnabled(EventLevel.Verbose, EventKeywords.None); }
        }

        public bool IsInfoEnabled
        {
            get { return this.IsEnabled(EventLevel.Informational, EventKeywords.None); }
        }

        public bool IsWarningEnabled
        {
            get { return this.IsEnabled(EventLevel.Warning, EventKeywords.None); }
        }

        public bool IsErrorEnabled
        {
            get { return this.IsEnabled(EventLevel.Error, EventKeywords.None); }
        }

        [Event(VerboseEventId, Level = EventLevel.Verbose)]
        public void Verbose(string message, string info)
        {
            this.WriteEvent(VerboseEventId, message, info);
        }

        [Event(InfoEventId, Level = EventLevel.Informational)]
        public void Info(string message, string info)
        {
            this.WriteEvent(InfoEventId, message, info);
        }

        [NonEvent]
        public void Warning(string message)
        {
            this.Warning(message, string.Empty);
        }

        [NonEvent]
        public void Warning(string message, Exception exception)
        {
            this.Warning(message, exception == null ? string.Empty : exception.ToString());
        }

        [Event(WarningEventId, Level = EventLevel.Warning)]
        public void Warning(string message, string exception)
        {
            this.WriteEvent(WarningEventId, message, exception);
        }

        [NonEvent]
        public void Error(string message, Exception exception)
        {
            this.Error(message, exception == null ? string.Empty : exception.ToString());
        }

        [Event(ErrorEventId, Level = EventLevel.Error)]
        public void Error(string message, string exception)
        {
            this.WriteEvent(ErrorEventId, message, exception);
        }
    }
}