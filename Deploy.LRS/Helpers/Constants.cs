namespace Deploy.Helpers
{
    public class Constants
    {
        public class Path
        {
            public static char[] SlashChars = new char[] { '/' };
            public const string SiteNameChars = "abcdfeghijklmnopqrstuvwxyz";
            public const string SiteNameNumbers = "0123456789";
        }

        public class Repository
        {
            public const string CustomTemplateFileName = "azuredeploy.json";
            public const string EmptySiteTemplateUrl = "http://deployredirector.azurewebsites.net/sitewithrepositoryv3.json";
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
            public const string WebsitesApiVersion = "2014-11-01";
            public const string GetScmDeploymentStatusFormat = "{0}/subscriptions/{1}/resourceGroups/{2}/providers/Microsoft.Web/sites/{3}/deployments?api-version={4}";
            public const string GetDeploymentStatusFormat = "{0}/subscriptions/{1}/resourcegroups/{2}/deployments/{2}/operations?api-version={3}";
        }

        /// <summary>
        /// Properties must be removed before submiting to ARM
        /// </summary>
        public class CustomTemplateProperties
        {
            public const string DefaultValueComeFirst = "defaultValueComeFirst";
        }
    }
}