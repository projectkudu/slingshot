using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Net.Mime;
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

        public static string WEBSITE_SITE_NAME { get { return config(); } }

        public static string BAYGeoRegions { get { return config("Central US,West US,South Central US,West US 2,West Central US,Canada East"); } }
        public static string BLUGeoRegions { get { return config("East US 2,North Central US,East US,Canada Central,Brazil South"); } }
        public static string DB3GeoRegions { get { return config("North Europe,West Europe,UK South,UK West"); } }
        public static string HK1GeoRegions { get { return config("East Asia,Southeast Asia,Japan West,Japan East,Australia East,Australia Southeast"); } }

        public static string ARMProviders { get { return config("Microsoft.Authorization,Microsoft.Features,Microsoft.Resources,Microsoft.Web,microsoft.support"); } }//Microsoft.Web,Microsoft.Features,Microsoft.Authorization,Microsoft.Resources,microsoft.support,Microsoft.NotificationHubs,microsoft.visualstudio,microsoft.insights
        public static string AppInsightsInstrumentationKey { get { return config(); } }
        public static string MixPanelInstrumentationKey { get { return config(); } }
        public static string AADClientId { get { return config(); } }
        public static string AADClientSecret { get { return config(); } }
        public static int SiteNamePostFixLength { get { return Int32.Parse("6"); } }
    }
}

