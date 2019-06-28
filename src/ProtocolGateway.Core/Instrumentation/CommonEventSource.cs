// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Instrumentation
{
    using System;
    using System.Diagnostics.Tracing;

    [EventSource(
        Name = "IoT-ProtocolGateway-Common",
        Guid = "06d7118e-3a71-4143-8aab-ed8cedf69e1c")]
    public class CommonEventSource : EventSource
    {
        const int VerboseEventId = 1;
        const int InfoEventId = 2;
        const int WarningEventId = 3;
        const int ErrorEventId = 4;

        public static readonly CommonEventSource Log = new CommonEventSource();

        CommonEventSource()
        {
        }

        public bool IsVerboseEnabled => this.IsEnabled(EventLevel.Verbose, EventKeywords.None);

        public bool IsInfoEnabled => this.IsEnabled(EventLevel.Informational, EventKeywords.None);

        public bool IsWarningEnabled => this.IsEnabled(EventLevel.Warning, EventKeywords.None);

        public bool IsErrorEnabled => this.IsEnabled(EventLevel.Error, EventKeywords.None);

        [NonEvent]
        public void Verbose(string message) => this.Verbose(message, string.Empty, string.Empty);

        [Event(VerboseEventId, Level = EventLevel.Verbose)]
        public void Verbose(string message, string channelId, string deviceId) => this.WriteEvent(VerboseEventId, message, channelId, deviceId);

        [Event(InfoEventId, Level = EventLevel.Informational)]
        public void Info(string message, string channelId, string deviceId) => this.WriteEvent(InfoEventId, message, channelId, deviceId);

        [NonEvent]
        public void Warning(string message, string channelId, string deviceId) => this.Warning(message, string.Empty, channelId, deviceId);

        [NonEvent]
        public void Warning(string message, Exception exception, string channelId, string deviceId) => this.Warning(message, exception?.ToString() ?? string.Empty, channelId, deviceId);

        [Event(WarningEventId, Level = EventLevel.Warning)]
        public void Warning(string message, string exception, string channelId, string deviceId) => this.WriteEvent(WarningEventId, message, exception, channelId, deviceId);

        [NonEvent]
        public void Error(string message, Exception exception, string channelId, string deviceId) => this.Error(message, exception?.ToString() ?? string.Empty, channelId, deviceId);

        [Event(ErrorEventId, Level = EventLevel.Error)]
        public void Error(string message, string exception, string channelId, string deviceId) => this.WriteEvent(ErrorEventId, message, exception, channelId, deviceId);
    }
}