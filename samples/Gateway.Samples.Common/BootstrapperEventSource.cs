﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Gateway.Samples.Common
{
    using System;
    using System.Diagnostics.Tracing;

    [EventSource(Name = "IoT-Gateway-Bootstrapper")]
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

        [Event(VerboseEventId)]
        public void Verbose(string message, string info)
        {
            if (this.IsVerboseEnabled)
            {
                this.WriteEvent(VerboseEventId, message, info);
            }
        }

        [Event(InfoEventId)]
        public void Info(string message, string info)
        {
            if (this.IsInfoEnabled)
            {
                this.WriteEvent(InfoEventId, message, info);
            }
        }

        [NonEvent]
        public void Warning(string message)
        {
            this.Warning(message, string.Empty);
        }

        [NonEvent]
        public void Warning(string message, Exception exception)
        {
            if (this.IsWarningEnabled)
            {
                this.Warning(message, exception == null ? string.Empty : exception.ToString());
            }
        }

        [Event(WarningEventId)]
        public void Warning(string message, string exception)
        {
            if (this.IsWarningEnabled)
            {
                this.WriteEvent(WarningEventId, message, exception);
            }
        }

        [NonEvent]
        public void Error(string message, Exception exception)
        {
            if (this.IsErrorEnabled)
            {
                this.Error(message, exception == null ? string.Empty : exception.ToString());
            }
        }

        [Event(ErrorEventId)]
        public void Error(string message, string exception)
        {
            if (this.IsErrorEnabled)
            {
                this.WriteEvent(ErrorEventId, message, exception);
            }
        }
    }
}