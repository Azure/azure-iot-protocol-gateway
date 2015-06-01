// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MqttClient
{
    using CommandLine;

    class Options
    {
        [Option('a', "address", DefaultValue = "127.0.0.1", HelpText = "address to connect to.")]
        public string Address { get; set; }

        [Option('p', "port", DefaultValue = 1883, HelpText = "port to connect to.")]
        public int Port { get; set; }

        [Option('t', "tls", DefaultValue = false, HelpText = "use TLS.")]
        public bool Tls { get; set; }
    }
}