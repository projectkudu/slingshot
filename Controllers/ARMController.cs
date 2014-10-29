using AzureDeployButton.Helpers;
using AzureDeployButton.Models;
using Microsoft.Azure.Management.Resources;
using Microsoft.Azure.Management.Resources.Models;
using Microsoft.Azure.Management.WebSites;
using Microsoft.WindowsAzure;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Routing;

namespace AzureDeployButton.Controllers
{
    public class ARMController : ApiController
    {
        private const string EmptySiteTemplateUrl = "https://dl.dropboxusercontent.com/u/2209341/EmptySite.json";

        static ARMController()
        {
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
        }

        public HttpResponseMessage GetToken()
        {
            var jwtToken = Request.Headers.GetValues("X-MS-OAUTH-TOKEN").FirstOrDefault();
            var base64 = jwtToken.Split('.')[1];

            // fixup
            int mod4 = base64.Length % 4;
            if (mod4 > 0)
            {
                base64 += new string('=', 4 - mod4);
            }

            var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
            var response = Request.CreateResponse(HttpStatusCode.OK);
            response.Content = new StringContent(json, Encoding.UTF8, "application/json");
            return response;
        }

        [Authorize]
        [HttpPost]
        public async Task<HttpResponseMessage> Deploy([FromBody] JObject parameters, string subscriptionId, string templateUrl)
        {
            CreateDeploymentResponse responseObj = new CreateDeploymentResponse();
            HttpResponseMessage response = null;

            try
            {
                // etodo: what should we do about this?
                //var resourceGroupName = "ehrg01";
                var resourceGroupName = GetParamOrDefault(parameters, "siteName", "mySite");

                using (var client = GetRMClient(subscriptionId))
                {
                    // For now we just default to East US for the resource group location.
                    var resourceResult = await client.ResourceGroups.CreateOrUpdateAsync(resourceGroupName, new BasicResourceGroup { Location = "East US" });
                    var templateParams = parameters.ToString();
                    var basicDeployment = new BasicDeployment
                    {
                        Parameters = templateParams,
                        TemplateLink = new TemplateLink(new Uri(templateUrl))
                    };

                    var deploymentResult = await client.Deployments.CreateOrUpdateAsync(resourceGroupName, resourceGroupName, basicDeployment);
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
        public async Task<HttpResponseMessage> GetDeploymentStatus(string subscriptionId, string siteName)
        {
            string provisioningState = null;
            string hostName = null;
            var responseObj = new JObject();

            using (var client = GetRMClient(subscriptionId))
            {
                var deployment = (await client.Deployments.GetAsync(siteName, siteName)).Deployment;
                provisioningState = deployment.Properties.ProvisioningState;
            }

            if (provisioningState == "Succeeded")
            {
                using (var wsClient = GetWSClient(subscriptionId))
                {
                    hostName = (await wsClient.WebSites.GetAsync(siteName, siteName, null, null)).WebSite.Properties.HostNames[0];
                }
            }

            responseObj["provisioningState"] = provisioningState;
            responseObj["siteUrl"] = string.Format("http://{0}", hostName);

            return Request.CreateResponse(HttpStatusCode.OK, responseObj);
        }

        [Authorize]
        [HttpGet]
        public async Task<HttpResponseMessage> IsSiteNameAvailable(string subscriptionId, string siteName)
        {
            string token = GetTokenFromHeader();
            bool isAvailable;

            using (var webSiteMgmtClient =
                CloudContext.Clients.CreateWebSiteManagementClient(new TokenCloudCredentials(subscriptionId, token)))
            {
                isAvailable = (await webSiteMgmtClient.WebSites.IsHostnameAvailableAsync(siteName)).IsAvailable;
            }

            return Request.CreateResponse(HttpStatusCode.OK, new { siteName = siteName, isAvailable = isAvailable });
        }

        [Authorize]
        [HttpGet]
        public async Task<HttpResponseMessage> GetTemplate()
        {
            // etodo: is there a way to get the route to match with an empty repositoryUrl so that I don't need this extra method?
            return await GetTemplate(null);
        }

        [Authorize]
        [HttpGet]
        public async Task<HttpResponseMessage> GetTemplate(string repositoryUrl)
        {
            HttpResponseMessage response = null;
            JObject template = null;
            string templateUrl = null;

            if (string.IsNullOrEmpty(repositoryUrl))
            {
                templateUrl = EmptySiteTemplateUrl;
                template = await DownloadTemplate(templateUrl);
            }
            else
            {
                Uri repositoryUri = new Uri(repositoryUrl);
                if (repositoryUri.Segments.Length >= 3)
                {
                    string user = repositoryUri.Segments[1].Trim(new char[] { '/' });
                    string repo = repositoryUri.Segments[2].Trim(new char[] { '/' });
                    templateUrl = string.Format("https://raw.githubusercontent.com/{0}/{1}/master/azuredeploy.json", user, repo);
                    template = await DownloadTemplate(templateUrl);

                    // If a user opens the README.md file, and then clicks on the button, the referrer address will look something
                    // like this:  https://github.com/user/repo/blob/master/README.md
                    if (repositoryUri.Segments.Length > 3)
                    {
                        repositoryUrl = string.Format("https://github.com/{0}/{1}", user, repo);
                    }
                }
                
                if (template == null)
                {
                    templateUrl = EmptySiteTemplateUrl;
                    template = await DownloadTemplate(templateUrl);
                }
            }

            if (template != null)
            {
                string token = GetTokenFromHeader();

                var subscriptions = (await TokenUtils.GetSubscriptionsAsync(TokenUtils.AzureEnvs.Prod, token))
                                    .Where(s => s.state == "Enabled").ToArray();

                if (subscriptions.Length >= 1)
                {
                    var locations = await GetSiteLocations(token, subscriptions);

                    JObject returnObj = new JObject();
                    returnObj["template"] = template;
                    returnObj["templateUrl"] = templateUrl;
                    returnObj["repositoryUrl"] = repositoryUrl;
                    returnObj["siteLocations"] = JArray.FromObject(locations);
                    returnObj["subscriptions"] = JArray.FromObject(subscriptions);
                    response = Request.CreateResponse(HttpStatusCode.OK, returnObj);
                }
                else
                {
                    response = Request.CreateResponse(HttpStatusCode.InternalServerError, "No available active subscriptions");
                }
            }
            else
            {
                response = Request.CreateResponse(HttpStatusCode.NotFound);
            }


            return response;
        }

        private async Task<IList<string>> GetSiteLocations(string token, SubscriptionInfo[] subscriptions)
        {
            using (var client = GetRMClient(token, subscriptions.FirstOrDefault().subscriptionId))
            {
                var websites = (await client.Providers.GetAsync("Microsoft.Web")).Provider;
                return websites.ResourceTypes.FirstOrDefault(rt => rt.Name == "sites").Locations;
            }
        }

        private async Task<JObject> DownloadTemplate(string templateUrl)
        {
            JObject template = null;
            using (HttpClient client = new HttpClient())
            {
                var templateResponse = await client.GetAsync(templateUrl);
                if (templateResponse.IsSuccessStatusCode)
                {
                    template = JObject.Parse(templateResponse.Content.ReadAsStringAsync().Result);
                }
            }

            return template;
        }

        private HttpClient GetClient(string baseUri)
        {
            var client = new HttpClient();
            client.BaseAddress = new Uri(baseUri);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Request.Headers.GetValues("X-MS-OAUTH-TOKEN").FirstOrDefault());
            return client;
        }

        private ResourceManagementClient GetRMClient(string subscriptionId)
        {
            var token = Request.Headers.GetValues("X-MS-OAUTH-TOKEN").FirstOrDefault();
            return GetRMClient(token, subscriptionId);
        }

        private WebSiteManagementClient GetWSClient(string subscriptionId)
        {
            var token = Request.Headers.GetValues("X-MS-OAUTH-TOKEN").FirstOrDefault();
            var creds = new TokenCloudCredentials(subscriptionId, token);
            return new WebSiteManagementClient(creds);
        }

        private ResourceManagementClient GetRMClient(string token, string subscriptionId)
        {
            var creds = new TokenCloudCredentials(subscriptionId, token);
            return new ResourceManagementClient(creds);
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
    }
}