// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.Gateway.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using DotNetty.Buffers;

    public class TcpScenarioMessage
    {
        public TcpScenarioMessage(int order, string name, bool isOut, int delay, int[][] omit = null, string content = null,
            Action<IByteBuffer, TcpScenarioMessage[]> contextUpdateAction = null)
        {
            this.Order = order;
            this.Name = name;
            this.Out = isOut;
            this.ContextUpdateAction = contextUpdateAction;
            this.Delay = TimeSpan.FromMilliseconds(delay);
            this.Content = content == null ? new byte[0] : content.Split(' ').Select(x => (byte)int.Parse(x, NumberStyles.HexNumber)).ToArray();
            this.VerificationMaskMap = omit == null
                ? new Dictionary<int, int>()
                : omit
                    .Select(bitref => bitref.Length == 1 ? new
                    {
                        byteIndex = bitref[0],
                        mask = 0xFF
                    } : new
                    {
                        byteIndex = bitref[0],
                        mask = 1 << bitref[1]
                    })
                    .GroupBy(x => x.byteIndex, x => x.mask)
                    .ToDictionary(x => x.Key, x => x.Aggregate(0xFF, (s, v) => s & ~v));
        }

        public string Name { get; set; }

        public int Order { get; set; }

        public bool Out { get; set; }

        public Action<IByteBuffer, TcpScenarioMessage[]> ContextUpdateAction { get; private set; }

        public TimeSpan Delay { get; set; }

        public byte[] Content { get; set; }

        public Dictionary<int, int> VerificationMaskMap { get; set; }
    }
}