// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Tests.Load
{
    using CommandLine;

    class Options
    {
        [OptionArray('r', "runners", HelpText = "runners configuration in a form of threes: name start-frequency count. Known scenarios are \"stable\", \"occasional\"")]
        public string[] Runners { get; set; }

        //[Option('m', "multiply", DefaultValue = 1, HelpText = "multiplication factor per scenario numbers.")]
        //public int Multiply { get; set; }

        //[Option('l', "latency", DefaultValue = 40, HelpText = "latency per connection, msec.")]
        //public int Latency { get; set; }

        [Option('a', "address", DefaultValue = "127.0.0.1", HelpText = "address to connect to.")]
        public string Address { get; set; }

        [Option('p', "port", DefaultValue = 8883, HelpText = "port to connect to.")]
        public int Port { get; set; }

        [Option('c', "connection", HelpText = "IoT Hub connection string (without DeviceId).")]
        public string IotHubConnectionString { get; set; }

        [Option('h', "hostname", HelpText = "Protocol Gateway host name.")]
        public string HostName { get; set; }

        [Option("createdevices", DefaultValue = 0, HelpText = "specifies number of test devices to create.")]
        public int CreateDeviceCount { get; set; }

        [Option('s', "devicesfrom", DefaultValue = 1, HelpText = "specifies initial counter value to use for test devices creation and initialization.")]
        public int DeviceStartingFrom { get; set; }

        [Option("devicenamepattern", DefaultValue = "testdevice_{0}", HelpText = "specifies name pattern to use for newly created devices. Use \"{0}\" as a placeholder for running counter.")]
        public string DeviceNamePattern { get; set; }

        [Option("devicekey", HelpText = "specifies primary key value to use for newly created devices.")]
        public string DeviceKey { get; set; }

        [Option("devicekey2", HelpText = "specifies secondary key value to use for newly created devices.")]
        public string DeviceKey2 { get; set; }
    }
}