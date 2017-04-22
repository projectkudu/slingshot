using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Http.Routing;
using Microsoft.Azure.Management.Resources;
using Microsoft.Azure.Management.Resources.Models;
using Microsoft.Azure.Management.WebSites;
using Microsoft.Rest;
using Microsoft.WindowsAzure;
using Newtonsoft.Json.Linq;
using Deploy.Concrete;
using Deploy.Helpers;
using Deploy.Models;
using System.Configuration;
using Deploy.Resources;

namespace Deploy.Controllers
{
    [UnhandledExceptionFilter]
    public class LRSARMController : ApiController
    {
        private const char base64Character62 = '+';
        private const char base64Character63 = '/';
        private const char base64UrlCharacter62 = '-';
        private const char base64UrlCharacter63 = '_';

        private static Dictionary<string, string> sm_providerMap;

        static LRSARMController()
        {
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

            Dictionary<string, string> providerMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                {"microsoft.web/sites", Server.Deployment_CreatingWebApp},
                {"microsoft.web/sites/config", Server.Deployment_UpdatingWebAppConfig},
                {"microsoft.web/sites/sourcecontrols", Server.Deployment_SettingupSourceControl},
                {"microsoft.web/serverfarms", Server.Deployment_CreatingWebHostingPlan}
            };

            sm_providerMap = providerMap;
        }

        [Authorize]
        public HttpResponseMessage GetToken(bool plainText = false)
        {
            if (plainText)
            {
                var jwtToken = Request.Headers.GetValues(Constants.Headers.X_MS_OAUTH_TOKEN).FirstOrDefault();
                var response = Request.CreateResponse(HttpStatusCode.OK);
                response.Content = new StringContent(jwtToken, Encoding.UTF8, "text/plain");
                return response;
            }
            else
            {
                var response = Request.CreateResponse(HttpStatusCode.OK);
                response.Content = new StringContent(GetClaims().ToString(), Encoding.UTF8, "application/json");
                return response;
            }
        }

        [Authorize]
        public async Task<HttpResponseMessage> Get()
        {
            IHttpRouteData routeData = Request.GetRouteData();
            string path = routeData.Values["path"] as string;
            if (String.IsNullOrEmpty(path))
            {
                var response = Request.CreateResponse(HttpStatusCode.Redirect);
                response.Headers.Location = new Uri(Path.Combine(Request.RequestUri.AbsoluteUri, "subscriptions"));
                return response;
            }

            using (var client = GetClient(Utils.GetCSMUrl(Request.RequestUri.Host)))
            {
                return await Utils.Execute(client.GetAsync(path + "?api-version=2014-04-01"));
            }
        }

        [Authorize]
        [HttpPost]
        public async Task<HttpResponseMessage> Deploy(DeployInputs inputs)
        {
            CreateDeploymentResponse responseObj = new CreateDeploymentResponse();
            HttpResponseMessage response = null;

            try
            {
                using (var client = GetRMClient(inputs.subscriptionId))
                {
                    await RegisterProviders(client);
                    await client.ResourceGroups.CreateOrUpdateAsync(
                        inputs.resourceGroup.name,
                        new ResourceGroup { Location = inputs.resourceGroup.location });

                    Deployment basicDeployment = await this.GetDeploymentPayload(inputs);

                    await client.Deployments.CreateOrUpdateAsync(
                        inputs.resourceGroup.name,
                        inputs.resourceGroup.name,
                        basicDeployment);
                    responseObj.Message = Server.Deployment_DeploymentStarted;
                    response = Request.CreateResponse(HttpStatusCode.OK, responseObj);
                }
            }
            catch (CloudException ex)
            {
                responseObj.Error = ex.ErrorMessage;
                responseObj.ErrorCode = ex.ErrorCode;
                response = Request.CreateResponse(HttpStatusCode.BadRequest, responseObj);
            }

            return response;
        }

        private async Task RegisterProviders(ResourceManagementClient client)
        {
            await Task.WhenAll(Settings.ARMProviders.Split(',').Select((provider) => client.Providers.RegisterAsync(provider)));
        }

        [Authorize]
        [HttpGet]
        public async Task<HttpResponseMessage> GetDeploymentStatus(string subscriptionId, string resourceGroup, string appServiceName = null)
        {
            string provisioningState = null;
            var responseObj = new JObject();
            using (var client = GetRMClient(subscriptionId))
            {
                var deployment = (await client.Deployments.GetAsync(resourceGroup, resourceGroup)).Deployment;
                provisioningState = deployment.Properties.ProvisioningState;
            }

            if (provisioningState == "Succeeded" || provisioningState == "Failed")
            {
                if (appServiceName == null)
                {
                    appServiceName = resourceGroup;
                }
                if (provisioningState == "Succeeded")
                {
                    responseObj["siteUrl"] = string.Format("https://{0}.azurewebsites.net", appServiceName);
                }
                Utils.FireAndForget($"{appServiceName}.azurewebsites.net");
                Utils.FireAndForget($"{appServiceName}.scm.azurewebsites.net");
            }
            else
            {
                using (var client = GetClient())
                {
                    string url = string.Format(
                        Constants.CSM.GetDeploymentStatusFormat,
                        Utils.GetCSMUrl(Request.RequestUri.Host),
                        subscriptionId,
                        resourceGroup,
                        Constants.CSM.ApiVersion);

                    var getOpResponse = await client.GetAsync(url);
                    responseObj["operations"] = AddLocalzedDeploymentResult(JObject.Parse(getOpResponse.Content.ReadAsStringAsync().Result));
                }
            }
            responseObj["provisioningState"] = provisioningState;
            return Request.CreateResponse(HttpStatusCode.OK, responseObj);
        }

        private JToken AddLocalzedDeploymentResult(JObject jObject)
        {
            if (jObject["value"] != null)
            {
                foreach (var operation in jObject["value"])
                {
                    operation["properties"]["targetResource"]["localizedMessage"] = new JValue(GetMappedValue(operation["properties"]["targetResource"]["resourceType"].Value<string>().ToLowerInvariant()));
                }
            }
            return jObject;
        }

        private string GetMappedValue(string resourceType)
        {
            var msg = sm_providerMap[resourceType];
            if (msg != null)
            {
                return msg;
            }
            else
            {
                return $"{Server.Deployment_Updating}  {resourceType}";
            }
        } 

        [Authorize]
        [HttpGet]
        public async Task<HttpResponseMessage> GetTemplate(string templateName)
        {
            templateName = HttpUtility.UrlDecode(templateName);

            HttpResponseMessage response = null;
            JObject returnObj = new JObject();
            string token = GetTokenFromHeader();

            Task<SubscriptionInfo[]> subscriptionTask = Utils.GetSubscriptionsAsync(Request.RequestUri.Host, token);

            await Task.WhenAll(subscriptionTask);

            var subscription = subscriptionTask.Result.Where(s => s.state == "Enabled").OrderBy(s => s.displayName).ToArray().FirstOrDefault();
            var email = GetHeaderValue(Constants.Headers.X_MS_CLIENT_PRINCIPAL_NAME);
            var userDisplayName = GetHeaderValue(Constants.Headers.X_MS_CLIENT_DISPLAY_NAME) ?? email;
            returnObj["email"] = email;
            returnObj["subscription"] = JObject.FromObject(subscription);
            returnObj["userDisplayName"] = userDisplayName;

            returnObj["repositoryUrl"] = templateName;

            var templateUrl = $"https://tryappservice.azure.com/api/armtemplate/{templateName}";
            returnObj["templateUrl"] = templateUrl;
            string resourceGroupName = null;
            if (!string.IsNullOrEmpty(subscription.subscriptionId))
            {
                resourceGroupName = await GenerateResourceGroupName(token, templateName, subscription);
            }
            returnObj["appServiceLocation"] = GetRandomLocationinGeoRegion();
            returnObj["resourceGroupName"] = resourceGroupName;
            returnObj["appServiceName"] = resourceGroupName;
            returnObj["templateName"] = templateName;
            returnObj["nextStatusMessage"] = Server.Deployment_DeploymentStarted;

            response = Request.CreateResponse(HttpStatusCode.OK, returnObj);

            return response;
        }

        private async Task<string> GenerateResourceGroupName(string token, string repoName, SubscriptionInfo subscription)
        {
            if (!string.IsNullOrEmpty(repoName))
            {
                bool isAvailable = false;
                var creds = new TokenCloudCredentials(subscription.subscriptionId, token);
                var rdfeBaseUri = new Uri(Utils.GetRDFEUrl(Request.RequestUri.Host));

                using (var webSiteMgmtClient = CloudContext.Clients.CreateWebSiteManagementClient(creds, rdfeBaseUri))
                {
                    // Make 4 attempts to get a random name (based on the repo name)
                    for (int i = 0; i < 4; i++)
                    {
                        string resourceGroupName = GenerateRandomResourceGroupName(repoName);
                        isAvailable = await IsAppServiceNameAvailable(webSiteMgmtClient, resourceGroupName);

                        if (isAvailable)
                        {
                            return resourceGroupName;
                        }
                    }
                }
            }

            return null;
        }

        private static async Task<bool> IsAppServiceNameAvailable(Microsoft.WindowsAzure.Management.WebSites.WebSiteManagementClient webSiteMgmtClient, string siteName)
        {
            try
            {
                return (await webSiteMgmtClient.WebSites.IsHostnameAvailableAsync(siteName)).IsAvailable;
            }
            catch (CloudException e)
            {
                // For Dreamspark subscriptions, RDFE is not available so we can't make this call.
                // For now, just return true. The better thing to do is to switch to an ARM friendly call
                if (e.ErrorCode == "ForbiddenError")
                {
                    return true;
                }

                // For other cases, rethrow
                throw;
            }
        }

        private string GenerateRandomResourceGroupName(string baseName, int length = 6)
        {
            // Underscores are not valid in site names, so use dashes instead
            // only keep letters, number and -
            // e.g "ab.cde@##$ghijk341234kjk-" --> "ab-cde----ghijk341234kjk-"
            baseName = Regex.Replace(baseName, "[^a-zA-Z0-9-]", "-", RegexOptions.CultureInvariant);
            // e.g "ab-cde----ghijk341234kjk-" --> "ab-cde-ghijk341234kjk-"
            baseName = Regex.Replace(baseName, "[-]{2,}", "-", RegexOptions.CultureInvariant);
            baseName += "-";

            Random random = new Random();

            var strb = new StringBuilder(baseName.Length + length);
            strb.Append(baseName);
            for (int i = 0; i < length; ++i)
            {
                strb.Append(Constants.Path.SiteNameChars[random.Next(Constants.Path.SiteNameChars.Length)]);
            }

            return strb.ToString();
        }

        private string GetHeaderValue(string name)
        {
            IEnumerable<string> values = null;
            if (Request.Headers.TryGetValues(name, out values))
            {
                return values.FirstOrDefault();
            }

            return null;
        }

        private JObject GetClaims()
        {
            var jwtToken = Request.Headers.GetValues(Constants.Headers.X_MS_OAUTH_TOKEN).FirstOrDefault();
            var base64 = jwtToken.Split('.')[1];

            // fixup
            int mod4 = base64.Length % 4;
            if (mod4 > 0)
            {
                base64 += new string('=', 4 - mod4);
            }

            // decode url escape char
            base64 = base64.Replace(base64UrlCharacter62, base64Character62);
            base64 = base64.Replace(base64UrlCharacter63, base64Character63);

            var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
            return JObject.Parse(json);
        }

        private string GetRandomLocationinGeoRegion()
        {
            var regionsList = Settings.BAYGeoRegions;
            switch (Settings.WEBSITE_SITE_NAME)
            {
                case "deploy-blu":
                    regionsList = Settings.BLUGeoRegions;
                    break;
                case "deploy-db3":
                    regionsList = Settings.DB3GeoRegions;
                    break;
                case "deploy-hk1":
                    regionsList = Settings.HK1GeoRegions;
                    break;
                default:
                    regionsList = Settings.BAYGeoRegions;
                    break;
            }
            var regions = regionsList.Split(',');
            return regions[new Random().Next(0, regions.Length)];
        }

        private HttpClient GetClient(string baseUri)
        {
            var client = new HttpClient();
            if (baseUri != null)
            {
                client.BaseAddress = new Uri(baseUri);
            }

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Request.Headers.GetValues("X-MS-OAUTH-TOKEN").FirstOrDefault());
            return client;
        }

        private HttpClient GetClient()
        {
            return GetClient(null);
        }

        private ResourceManagementClient GetRMClient(string subscriptionId)
        {
            var token = Request.Headers.GetValues("X-MS-OAUTH-TOKEN").FirstOrDefault();
            return GetRMClient(token, subscriptionId);
        }

        private ResourceManagementClient GetRMClient(string token, string subscriptionId)
        {
            var creds = new Microsoft.Azure.TokenCloudCredentials(subscriptionId, token);
            return new ResourceManagementClient(creds, new Uri(Utils.GetCSMUrl(Request.RequestUri.Host)));
        }

        private string GetTokenFromHeader()
        {
            return Request.Headers.GetValues("X-MS-OAUTH-TOKEN").FirstOrDefault();
        }

        private async Task<Deployment> GetDeploymentPayload(DeployInputs inputs)
        {
            var basicDeployment = new Deployment();

            basicDeployment.Properties = new DeploymentProperties
            {
                Parameters = inputs.parameters.ToString(),
                TemplateLink = new TemplateLink(new Uri(inputs.templateUrl))
            };
            return basicDeployment;
        }

    }
}