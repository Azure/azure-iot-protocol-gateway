// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.Gateway.Tests
{
    using System.ComponentModel;
    using System.Net;

    public class TcpScenarioOptions
    {
        public TcpScenarioOptions()
        {
            this.ClientCount = 1;
            this.Port = 1883;
            this.Address = IPAddress.Loopback;
            this.RepeatCount = 1;
            this.StartDelay = 2000;
            this.EndDelay = 2000;
            this.ReportInterval = 5;
        }

        /// <summary>
        /// IP Address to connect to
        /// </summary>
        [DefaultValue("127.0.0.1")]
        public IPAddress Address { get; set; }

        /// <summary>
        /// Port to connect to
        /// </summary>
        [DefaultValue(1883)]
        public int Port { get; set; }

        /// <summary>
        /// Number of client connections
        /// </summary>
        [DefaultValue(1)]
        public int ClientCount { get; set; }

        /// <summary>
        /// number of times to run scenario
        /// </summary>
        [DefaultValue(1)]
        public int RepeatCount { get; set; }

        /// <summary>
        /// Whether to use TLS when establishing connection to service
        /// </summary>
        [DefaultValue(false)]
        public bool Tls { get; set; }

        /// <summary>
        /// Initial delay in msec after connection is established.
        /// </summary>
        [DefaultValue(2000)]
        public int StartDelay { get; set; }

        /// <summary>
        /// Final delay before connection is closed.
        /// </summary>
        [DefaultValue(2000)]
        public int EndDelay { get; set; }

        /// <summary>
        /// Interval of reporting latency + throughput, sec.
        /// </summary>
        [DefaultValue(5)]
        public int ReportInterval { get; set; }
    }
}