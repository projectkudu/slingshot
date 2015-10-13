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
            public const string CustomTemplateFileName = "azuredeploy.json";
            public const string EmptySiteTemplateUrl = "http://deployredirector.azurewebsites.net/sitewithrepositoryv3.json";
            public const string GitCustomTemplateFolderUrlFormat = "https://raw.githubusercontent.com/{0}/{1}/{2}/";
            public const string GitHubApiRepoInfoFormat = "https://api.github.com/repos/{0}/{1}";

            public const string BitbucketWebRepoInfoFormat = "https://bitbucket.org/{0}/{1}";
            public const string BitbucketApiRepoInfoFormat = "https://api.bitbucket.org/2.0/repositories/{0}/{1}";
            public const string BitbucketApiPullRequestInfoFormat = "https://api.bitbucket.org/2.0/repositories/{0}/{1}/pullrequests/{2}";
            public const string BitbucketRawFileWebFormat = "https://bitbucket.org/{0}/{1}/raw/{2}/{3}";
            public const string BitbucketApiMainBranchInfoFormat = "https://bitbucket.org/api/1.0/repositories/{0}/{1}/main-branch";
            public const string BitbucketApiPullRequestCommentsFormat = "https://bitbucket.org/api/1.0/repositories/{0}/{1}/pullrequests/{2}/comments";
            public const string BitbucketApiRawFile = "https://bitbucket.org/api/1.0/repositories/{0}/{1}/raw/{2}/{3}";
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
    }
}