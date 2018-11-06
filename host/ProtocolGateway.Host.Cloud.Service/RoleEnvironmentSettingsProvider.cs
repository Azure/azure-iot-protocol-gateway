// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ProtocolGateway.Host.Cloud.Service
{
    using Microsoft.Azure.Devices.ProtocolGateway;
    using Microsoft.WindowsAzure.ServiceRuntime;

    public class RoleEnvironmentSettingsProvider : ISettingsProvider
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