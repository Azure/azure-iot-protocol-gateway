// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway
{
    using Microsoft.Extensions.Configuration;

    public class ConfigurationExtensionReader : IAppConfigReader
    {
        static readonly IConfiguration Config = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();

        public bool TryGetSetting(string name, out string value)
        {
            IConfigurationSection appsettings = Config.GetSection("AppSettings");
            value = appsettings.GetSection(name).Value;
            if (value == null)
            {
                value = default(string);
                return false;
            }

            return true;
        }
    }
}