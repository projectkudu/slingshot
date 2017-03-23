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
using Slingshot.Concrete;
using Slingshot.Helpers;
using Slingshot.Models;

namespace Slingshot.Controllers
{
    [UnhandledExceptionFilter]
    public class ARMController : ApiController
    {
        private const char base64Character62 = '+';
        private const char base64Character63 = '/';
        private const char base64UrlCharacter62 = '-';
        private const char base64UrlCharacter63 = '_';

        private static Dictionary<string, string> sm_providerMap;

        static ARMController()
        {
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

            Dictionary<string, string> providerMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                {"Microsoft.Web", "Website"},
                {"Microsoft.cache", "Redis Cache"},
                {"Microsoft.DocumentDb", "DocumentDB"},
                {"Microsoft.Insights", "Application Insights"},
                {"Microsoft.Search", "Search"},
                {"SuccessBricks.ClearDB", "ClearDB"},
                {"Microsoft.BizTalkServices","Biz Talk Services"},
                {"Microsoft.Sql","SQL Azure"},
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
                    // For now we just default to East US for the resource group location.
                    var resourceResult = await client.ResourceGroups.CreateOrUpdateAsync(
                        inputs.resourceGroup.name,
                        new ResourceGroup { Location = inputs.resourceGroup.location });

                    var templateParams = inputs.parameters.ToString();
                    Deployment basicDeployment = await this.GetDeploymentPayload(inputs);

                    var deploymentResult = await client.Deployments.CreateOrUpdateAsync(
                        inputs.resourceGroup.name,
                        inputs.resourceGroup.name,
                        basicDeployment);

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

        [Authorize]
        [HttpGet]
        public async Task<HttpResponseMessage> GetDeploymentStatus(string subscriptionId, string resourceGroup, string appServiceName = null)
        {
            string provisioningState = null;
            string hostName = null;
            var responseObj = new JObject();

            using (var client = GetRMClient(subscriptionId))
            {
                var deployment = (await client.Deployments.GetAsync(resourceGroup, resourceGroup)).Deployment;
                provisioningState = deployment.Properties.ProvisioningState;
            }

            using (var client = GetClient())
            {
                string url = string.Format(
                    Constants.CSM.GetDeploymentStatusFormat,
                    Utils.GetCSMUrl(Request.RequestUri.Host),
                    subscriptionId,
                    resourceGroup,
                    Constants.CSM.ApiVersion);

                var getOpResponse = await client.GetAsync(url);
                responseObj["operations"] = JObject.Parse(getOpResponse.Content.ReadAsStringAsync().Result);
            }

            if (provisioningState == "Succeeded")
            {
                    responseObj["siteUrl"] = string.Format("https://{0}.azurewebsites.net", appServiceName);
            }

            responseObj["provisioningState"] = provisioningState;

            return Request.CreateResponse(HttpStatusCode.OK, responseObj);
        }

        [Authorize]
        [HttpGet]
        public async Task<HttpResponseMessage> GetScmDeploymentStatus(string subscriptionId, string resourceGroup, string siteName)
        {
            HttpResponseMessage response = null;
            using (var client = GetClient())
            {
                string url = string.Format(
                    Constants.CSM.GetScmDeploymentStatusFormat,
                    Utils.GetCSMUrl(Request.RequestUri.Host),
                    subscriptionId,
                    resourceGroup,
                    siteName,
                    Constants.CSM.WebsitesApiVersion);

                for (int i = 0; i < 5; i++)
                {
                    var statusResponse = await client.GetAsync(url);
                    if (statusResponse.IsSuccessStatusCode)
                    {
                        var resultObj = JObject.Parse(await statusResponse.Content.ReadAsStringAsync());
                        var deployments = resultObj["properties"];
                        if (deployments.Count() > 0)
                        {
                            response = Request.CreateResponse(HttpStatusCode.OK, deployments.First());
                            break;
                        }

                        await Task.Delay(1000);
                    }
                    else
                    {
                        response = statusResponse;
                    }
                }

                if (response == null)
                {
                    response = Request.CreateResponse(HttpStatusCode.NotFound, new { error = "Could not find any source control deployments" });
                }
            }

            return response;
        }

        [Authorize]
        [HttpGet]
        public async Task<HttpResponseMessage> IsAppServiceNameAvailable(string subscriptionId, string appServiceName)
        {
            string token = GetTokenFromHeader();
            bool isAvailable;

            using (var webSiteMgmtClient =
                CloudContext.Clients.CreateWebSiteManagementClient(new TokenCloudCredentials(subscriptionId, token)))
            {
                isAvailable = await IsAppServiceNameAvailable(webSiteMgmtClient, appServiceName);
            }

            return Request.CreateResponse(HttpStatusCode.OK, new { appServiceName = appServiceName, isAvailable = isAvailable });
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
            //Task<JArray> tenantTask = GetTenantsArray();
            await Task.WhenAll(subscriptionTask);

            var subscriptions = subscriptionTask.Result.Where(s => s.state == "Enabled").OrderBy(s => s.displayName).ToArray();
            var email = GetHeaderValue(Constants.Headers.X_MS_CLIENT_PRINCIPAL_NAME);
            var userDisplayName = GetHeaderValue(Constants.Headers.X_MS_CLIENT_DISPLAY_NAME) ?? email;

            returnObj["subscriptions"] = JArray.FromObject(subscriptions);
            returnObj["userDisplayName"] = userDisplayName;

            returnObj["repositoryUrl"] = templateName;
            returnObj["repositoryDisplayUrl"] = templateName;
            returnObj["isManualIntegration"] = true;

            var queryStrings = HttpUtility.ParseQueryString(templateName);
            bool isManualIntegration = true;
            if (bool.TryParse(queryStrings["manual"], out isManualIntegration))
            {
                returnObj["isManualIntegration"] = isManualIntegration;
            }

            var templateUrl = $"https://tryappservice.azure.com/api/armtemplate/{templateName}";
            Task<JObject> getTemplateTask = HttpClientUtils.DownloadJson(templateUrl);
            await Task.WhenAll(getTemplateTask);

            JObject template = getTemplateTask.Result;
            if (template != null)
            {
                string resourceGroupName = null;
                if (subscriptions.Length >= 1)
                {
                    await GetLocations(template, returnObj, token, subscriptions);
                    resourceGroupName = await GenerateResourceGroupName(token, templateName, subscriptions);
                }

                returnObj["resourceGroupName"] = resourceGroupName;
                returnObj["template"] = template;
                returnObj["templateName"] = templateName;
                returnObj["templateUrl"] = templateUrl;

                // Check if the template takes in a Website parameter
                if (template["parameters"]["appServiceName"] != null)
                {
                    // Set the default site name to the same as the rg name
                    returnObj["appServiceName"] = resourceGroupName;
                }

                response = Request.CreateResponse(HttpStatusCode.OK, returnObj);
            }
            else
            {
                returnObj["error"] = string.Format("Could not find the Azure RM Template '{0}'", templateName);
                response = Request.CreateResponse(HttpStatusCode.NotFound, returnObj);
            }

            return response;
        }

        private async Task<string> GenerateResourceGroupName(string token, string repoName, SubscriptionInfo[] subscriptions)
        {
            if (!string.IsNullOrEmpty(repoName))
            {
                bool isAvailable = false;
                var creds = new TokenCloudCredentials(subscriptions.First().subscriptionId, token);
                var rdfeBaseUri = new Uri(Utils.GetRDFEUrl(Request.RequestUri.Host));

                using (var webSiteMgmtClient = CloudContext.Clients.CreateWebSiteManagementClient(creds, rdfeBaseUri))
                {
                    // Make 3 attempts to get a random name (based on the repo name)
                    for (int i = 0; i < 3; i++)
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
        
        private HttpResponseMessage Transfer(HttpResponseMessage response)
        {
            var ellapsed = response.Headers.GetValues(Constants.Headers.X_MS_Ellapsed).First();
            response = Request.CreateResponse(response.StatusCode);
            response.Headers.Add(Constants.Headers.X_MS_Ellapsed, ellapsed);
            return response;
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

        private async Task GetLocations(
            JObject template,
            JObject returnObj,
            string token,
            SubscriptionInfo[] subscriptions)
        {
            IEnumerable<string> locations = null;

            using (var client = GetRMClient(token, subscriptions.FirstOrDefault().subscriptionId))
            {
                var websites = (await client.Providers.GetAsync("Microsoft.Web")).Provider;
                locations = websites.ResourceTypes.FirstOrDefault(rt => rt.Name == "sites").Locations
                       .Where(location => location.IndexOf("MSFT", StringComparison.OrdinalIgnoreCase) < 0);

            }

            returnObj["appServiceLocations"] = JArray.FromObject(locations);
            return;
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

        private WebSiteManagementClient GetWSClient(string subscriptionId)
        {
            var token = Request.Headers.GetValues("X-MS-OAUTH-TOKEN").FirstOrDefault();
            var tokenCreds = new TokenCredentials(token);
            var client = new WebSiteManagementClient(new Uri(Utils.GetCSMUrl(Request.RequestUri.Host)), tokenCreds);
            client.SubscriptionId = subscriptionId;
            return client;
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

        private static string GetParamOrDefault(JObject parameters, string paramName, string defaultValue)
        {
            string paramValue = null;
            var param = parameters[paramName];
            if (param != null)
            {
                paramValue = param["value"].Value<string>() ?? defaultValue;
            }
            return paramValue;
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

        private static void PurgeCustomProperties(JObject template)
        {
            JObject paramObjFromTmpl = template.Value<JObject>("parameters");
            if (paramObjFromTmpl != null)
            {
                foreach (var p in paramObjFromTmpl)
                {
                    if (paramObjFromTmpl[p.Key] != null && paramObjFromTmpl[p.Key][Constants.CustomTemplateProperties.DefaultValueComeFirst] != null)
                    {
                        paramObjFromTmpl[p.Key][Constants.CustomTemplateProperties.DefaultValueComeFirst].Parent.Remove();
                    }
                }
            }
        }
    }
}