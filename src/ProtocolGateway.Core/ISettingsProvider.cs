// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
#if !NETSTANDARD1_3
    using System.Configuration;
#endif

    public interface ISettingsProvider
    {
        bool TryGetSetting(string name, out string value);
    }

    public static class SettingsProviderExtensions
    {
        public static string GetSetting(this ISettingsProvider settingsProvider, string name, string defaultValue = null)
        {
            string result;
            if (settingsProvider.TryGetSetting(name, out result))
            {
                return result;
            }

            if (defaultValue != null)
            {
                return defaultValue;
            }

#if NETSTANDARD1_3  
            throw new ConfigurationErrorsException(name);
#else
            Trace.TraceError("Setting could not be found: " + name);
            throw new ConfigurationErrorsException(name);
#endif
        }

        public static int GetIntegerSetting(this ISettingsProvider settingsProvider, string name, int? defaultValue = null)
        {
            int result;
            if (TryGetIntegerSetting(settingsProvider, name, out result))
            {
                return result;
            }

            if (defaultValue.HasValue)
            {
                return defaultValue.Value;
            }

#if NETSTANDARD1_3
            
            throw new ConfigurationErrorsException(name);
#else
            Trace.TraceError("Error converting configuration value to integer: " + name);
            throw new ConfigurationErrorsException(name);
#endif
        }

        public static bool TryGetIntegerSetting(this ISettingsProvider settingsProvider, string name, out int result)
        {
            string value;
            if (!settingsProvider.TryGetSetting(name, out value))
            {
                result = default(int);
                return false;
            }

            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
        }

        public static bool GetBooleanSetting(this ISettingsProvider settingsProvider, string name, bool? defaultValue = null)
        {
            bool result;
            if (TryGetBooleanSetting(settingsProvider, name, out result))
            {
                return result;
            }

            if (defaultValue.HasValue)
            {
                return defaultValue.Value;
            }

#if NETSTANDARD1_3
            
            throw new ConfigurationErrorsException(name);
#else
            Trace.TraceError("Error converting configuration value to bool: " + name);
            throw new ConfigurationErrorsException(name);
#endif
        }

        public static bool TryGetBooleanSetting(this ISettingsProvider settingsProvider, string name, out bool result)
        {
            string value;
            if (!settingsProvider.TryGetSetting(name, out value))
            {
                result = default(bool);
                return false;
            }

            return bool.TryParse(value, out result);
        }

        public static TimeSpan GetTimeSpanSetting(this ISettingsProvider settingsProvider, string name, TimeSpan? defaultValue = null)
        {
            TimeSpan result;
            if (TryGetTimeSpanSetting(settingsProvider, name, out result))
            {
                return result;
            }

            if (defaultValue.HasValue)
            {
                return defaultValue.Value;
            }
#if NETSTANDARD1_3
            
            throw new ConfigurationErrorsException(name);
#else
            Trace.TraceError("Error converting configuration value to TimeSpan: " + name);
            throw new ConfigurationErrorsException(name);
#endif
        }

        public static bool TryGetTimeSpanSetting(this ISettingsProvider settingsProvider, string name, out TimeSpan result)
        {
            string value;
            if (!settingsProvider.TryGetSetting(name, out value))
            {
                result = TimeSpan.Zero;
                return false;
            }

            return TimeSpan.TryParse(value, out result);
        }
    }
}