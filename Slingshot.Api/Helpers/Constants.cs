using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Slingshot.Helpers
{
    public class Constants
    {
        public class Path
        {
            public static char[] SlashChars = new char[] { '/' };
            public const string HexChars = "0123456789abcdfe";
        }

        public class Repository
        {
            public const string EmptySiteTemplateUrl = "http://deploytoazure.azurewebsites.net/sitewithrepository.json";
            public const string GitCustomTemplateFormat = "https://raw.githubusercontent.com/{0}/{1}/{2}/azuredeploy.json";
            public const string GitHubApiRepoInfoFormat = "https://api.github.com/repos/{0}/{1}";
        }

        public class Headers
        {
            public const string X_MS_OAUTH_TOKEN = "X-MS-OAUTH-TOKEN";
            public const string X_MS_Ellapsed = "X-MS-Ellapsed";
            public const string X_MS_CLIENT_PRINCIPAL_NAME = "X-MS-CLIENT-PRINCIPAL-NAME";
            public const string X_MS_CLIENT_DISPLAY_NAME = "X-MS-CLIENT-DISPLAY-NAME";
        }

        public class CSM
        {
            public const string ApiVersion = "2014-04-01";
            public const string GetGitDeploymentStatusFormat = "{0}/subscriptions/{1}/resourceGroups/{2}/providers/Microsoft.Web/sites/{3}/deployments?api-version={4}";
            public const string GetDeploymentStatusFormat = "{0}/subscriptions/{1}/resourcegroups/{2}/deployments/{2}/operations?api-version={3}";
        }
    }
}