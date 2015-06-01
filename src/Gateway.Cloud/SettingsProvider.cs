// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.Gateway.Cloud
{
    using Microsoft.Azure.Devices.Gateway.Core;
    using Microsoft.WindowsAzure.ServiceRuntime;

    public class SettingsProvider : ISettingsProvider
    {
        public bool TryGetSetting(string name, out string value)
        {
            try
            {
                value = RoleEnvironment.GetConfigurationSettingValue(name);
                return true;
            }
            catch (RoleEnvironmentException)
            {
                value = default(string);
                return false;
            }
        }
    }
}