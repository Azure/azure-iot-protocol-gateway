// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ProtocolGateway.Host.Common
{
    using Microsoft.Azure.Devices.ProtocolGateway;
    using Microsoft.Extensions.Configuration;

    public class ConfigurationSettingsProvider : ISettingsProvider
    {
        readonly IConfigurationSection config;

        // static readonly IConfiguration Config = ;

        public ConfigurationSettingsProvider(IConfigurationSection configSection)
        {
            this.config = configSection;            
        }

        public bool TryGetSetting(string name, out string value)
        {
            // IConfigurationSection appsettings = config.GetSection("AppSettings");
            value = this.config.GetSection(name).Value;
            if (value == null)
            {
                value = default(string);
                return false;
            }

            return true;
        }
    }
}