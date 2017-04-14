// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway
{
    using System.Configuration;

    class ConfigManagerReader : IAppConfigReader
    {
        public bool TryGetSetting(string name, out string value)
        {
            string[] values = ConfigurationManager.AppSettings.GetValues(name);
            if (values == null || values.Length == 0)
            {
                value = default(string);
                return false;
            }

            value = values[0];
            return true;
        }
    }
}
