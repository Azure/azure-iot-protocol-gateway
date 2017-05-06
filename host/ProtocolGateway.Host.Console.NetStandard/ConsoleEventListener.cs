// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ProtocolGateway.Host.Console.NetStandard
{
    using System.Diagnostics.Tracing;
    using System.Text;

    class ConsoleEventListener : EventListener
    {
        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            StringBuilder payload = new StringBuilder();
            foreach (object p in eventData.Payload)
            {
                payload.Append("[" + p + "]");
            }
            System.Console.WriteLine("EventId: {0}, Level: {1}, Message: {2}, Payload: {3} , EventName: {4}", eventData.EventId, eventData.Level, eventData.Message, payload.ToString(), eventData.EventName);
        }
    }
}
