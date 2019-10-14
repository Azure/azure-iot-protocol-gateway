// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Mqtt
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Tracks message order upon arrival and triggers completion action in the same order as messages had arrived.
    /// </summary>
    public sealed class AckQueue
    {
        sealed class Entry
        {
            public Entry(int packetId)
            {
                this.PacketId = packetId;
            }

            public int PacketId { get; }
            public bool Completed { get; private set; }

            public void Complete()
            {
                this.Completed = true;
            }
        }

        readonly Queue<Entry> ackQueue;
        readonly Action<int> sendAction;

        public int Count => this.ackQueue.Count;

        public AckQueue(Action<int> completeAction)
        {
            this.ackQueue = new Queue<Entry>();
            this.sendAction = completeAction;
        }

        public async void Post(int packetId, Task future)
        {
            try
            {
                var entry = new Entry(packetId);
                this.ackQueue.Enqueue(entry);
                await future;
                if (this.ackQueue.Count > 0)
                {
                    if (this.ackQueue.Peek().PacketId == packetId) {
                        this.sendAction(packetId);
                        this.ackQueue.Dequeue();
                        while (this.ackQueue.Count > 0 && this.ackQueue.Peek().Completed) {
                            var next = this.ackQueue.Dequeue();
                            this.sendAction(next.PacketId);
                        }
                    }
                    else {
                        entry.Complete();
                    }
                }

            }
            catch (Exception)
            { 
                // Queue does not handle failures, they must be tracked elsewhere
            }
        }
    }
}