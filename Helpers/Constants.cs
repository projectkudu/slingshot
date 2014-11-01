using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace AzureDeployButton.Helpers
{
    public class Constants
    {
        public class Path
        {
            public static char[] SlashChars = new char[] { '/' };
        }

        public class Repository
        {
            public const string EmptySiteTemplateUrl = "https://raw.githubusercontent.com/Tuesdaysgreen/HelloWorld/master/siteWithRepository.json";
            public const string GitCustomTemplateFormat = "https://raw.githubusercontent.com/{0}/{1}/{2}/azuredeploy.json";
        }

        public class CSMUrls
        {
            public const string GitDeploymentStatusFormat = "https://management.azure.com/subscriptions/{0}/resourceGroups/{1}/providers/Microsoft.Web/sites/{1}/deployments?api-version=2014-06-01";
        }
    }
}