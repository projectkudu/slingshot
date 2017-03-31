using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Web;

namespace Deploy
{
    public static class Settings
    {
        private static string config(string @default = null, [CallerMemberName] string key = null)
        {
            var value = System.Environment.GetEnvironmentVariable(key) ?? ConfigurationManager.AppSettings[key];
            return string.IsNullOrEmpty(value)
                ? @default
                : value;
        }

        public static string GeoRegions { get { return config("East US,West US,North Europe,West Europe,South Central US,North Central US,East Asia,Southeast Asia,Japan West,Japan East,Brazil South"); } }

        public static string AppInsightsInstrumentationKey { get { return config("f7b52ae5-8231-45dd-b0a5-c34083ca3e5e"); } }
        public static string MixPanelInstrumentationKey { get { return config("1d92e3abd14a2d65f748d1314dd24b99"); } }
        public static string AADClientId { get { return config(); } }
        public static string AADClientSecret { get { return config(); } }
    }

}