using AzureDeployButton.Abstract;
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
using System.IO;
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

            if (path.StartsWith("tenants", StringComparison.OrdinalIgnoreCase))
            {
                return await GetTenants(path);
            }

            using (var client = GetClient(Utils.GetCSMUrl(Request.RequestUri.Host)))
            {
                return await Utils.Execute(client.GetAsync(path + "?api-version=2014-04-01"));
            }
        }

        [Authorize]
        [HttpPost]
        public async Task<HttpResponseMessage> Preview([FromBody] JObject parameters, string subscriptionId, string templateUrl)
        {
            JObject responseObj = new JObject();
            List<string> providers = new List<string>(32);
            HttpResponseMessage response = null;

            using (var client = GetRMClient(subscriptionId))
            {
                ResourceGroupCreateOrUpdateResult resourceResult = null;
                string tempRGName = Guid.NewGuid().ToString();

                try
                {
                    resourceResult = await client.ResourceGroups.CreateOrUpdateAsync(tempRGName, new BasicResourceGroup { Location = "East US" });

                    // For now we just default to East US for the resource group location.
                    var basicDeployment = new BasicDeployment
                    {
                        Parameters = parameters.ToString(),
                        TemplateLink = new TemplateLink(new Uri(templateUrl))
                    };

                    var deploymentResult = await client.Deployments.ValidateAsync(tempRGName, tempRGName, basicDeployment);
                    if (deploymentResult.StatusCode == HttpStatusCode.OK)
                    {
                        foreach (var p in deploymentResult.Properties.Providers)
                        {
                            if (sm_providerMap.ContainsKey(p.Namespace))
                            {
                                providers.Add(sm_providerMap[p.Namespace]);
                            }
                            else
                            {
                                providers.Add(p.Namespace);
                            }
                        }

                        responseObj["providers"] = JArray.FromObject(providers);
                        response = Request.CreateResponse(HttpStatusCode.OK, responseObj);
                    }
                    else
                    {
                        response = Request.CreateResponse(deploymentResult.StatusCode);
                    }
                }
                finally
                {
                    if (resourceResult != null &&
                        (resourceResult.StatusCode == HttpStatusCode.Created || resourceResult.StatusCode == HttpStatusCode.OK))
                    {
                        string token = GetTokenFromHeader();
                        Task.Run(() => { DeleteResourceGroup(subscriptionId, token, tempRGName); });
                    }
                }
            }

            return response;
        }

        // Called from a threadpool thread
        private void DeleteResourceGroup(string subscriptionId, string token, string rgName)
        {
            try
            {
                using (var client = GetRMClient(token, subscriptionId))
                {
                    var delResult = client.ResourceGroups.Delete(rgName);
                }
            }
            catch
            {
            }
        }

        [Authorize]
        [HttpPost]
        public async Task<HttpResponseMessage> Deploy([FromBody] JObject parameters, string subscriptionId, string templateUrl)
        {
            CreateDeploymentResponse responseObj = new CreateDeploymentResponse();
            HttpResponseMessage response = null;

            try
            {
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
        public async Task<HttpResponseMessage> GetGitDeploymentStatus(string subscriptionId, string siteName)
        {
            HttpResponseMessage response = null;
            using (var client = GetClient())
            {
                var statusResponse = await client.GetAsync(
                    string.Format(Constants.CSMUrls.GitDeploymentStatusFormat, subscriptionId, siteName));

                if (statusResponse.IsSuccessStatusCode)
                {
                    var resultObj = JObject.Parse(await statusResponse.Content.ReadAsStringAsync());
                    var deployments = resultObj["properties"];
                    if (deployments.Count() > 0)
                    {
                        response = Request.CreateResponse(HttpStatusCode.OK, deployments.First());
                    }
                    else
                    {
                        response = Request.CreateResponse(HttpStatusCode.NotFound, new { error = "Could not find any git deployments" });
                    }
                }
                else
                {
                    response = statusResponse;
                }
            }

            return response;
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
        public async Task<HttpResponseMessage> GetTemplate(string repositoryUrl)
        {
            HttpResponseMessage response = null;
            string branch = null;

            Repository repo = Repository.CreateRepositoryObj(repositoryUrl);
            repositoryUrl = repo.RepositoryUrl;

            string templateUrl = await repo.GetTemplateUrlAsync();
            JObject template = await repo.DownloadTemplateAsync();

            // BUG: There's an Antares Bug that prevents us from, being able to deploy any branch other than
            // master so we'll hard-code it for now.
            //branch = repo.Branch;
            branch = "master";

            if (template != null)
            {
                string token = GetTokenFromHeader();

                var subscriptions = (await TokenUtils.GetSubscriptionsAsync(TokenUtils.AzureEnvs.Prod, token))
                                    .Where(s => s.state == "Enabled").ToArray();

                var tenants = await GetTenantsArray();
                var email = GetHeaderValue("X-MS-CLIENT-PRINCIPAL-NAME");
                var userDisplayName = GetHeaderValue("X-MS-CLIENT-DISPLAY-NAME") ?? email;

                if (subscriptions.Length >= 1)
                {
                    var locations = await GetSiteLocations(token, subscriptions);

                    JObject returnObj = new JObject();
                    returnObj["template"] = template;
                    returnObj["templateUrl"] = templateUrl;
                    returnObj["repositoryUrl"] = repositoryUrl;
                    returnObj["branch"] = branch;
                    returnObj["siteLocations"] = JArray.FromObject(locations);
                    returnObj["subscriptions"] = JArray.FromObject(subscriptions);
                    returnObj["tenants"] = tenants;
                    returnObj["userDisplayName"] = userDisplayName;
                    response = Request.CreateResponse(HttpStatusCode.OK, returnObj);
                }
                else
                {
                    response = Request.CreateResponse(
                        HttpStatusCode.InternalServerError,
                        "No available active subscriptions");
                }
            }
            else
            {
                response = Request.CreateResponse(
                    HttpStatusCode.NotFound,
                    string.Format("Could not find the Azure RM Template '{0}'", repositoryUrl));
            }

            return response;
        }

        private string GetHeaderValue(string name)
        {
            IEnumerable<string> values = null;
            if(Request.Headers.TryGetValues(name, out values))
            {
                return values.FirstOrDefault();
            }

            return null;
        }

        private async Task<JArray> GetTenantsArray()
        {

            if (!Request.RequestUri.IsLoopback)
            {
                using (var client = GetClient(Request.RequestUri.GetLeftPart(UriPartial.Authority)))
                {
                    var response = await Utils.Execute(client.GetAsync("tenantdetails"));
                    if (!response.IsSuccessStatusCode)
                    {
                        return null;
                    }

                    var tenants = await response.Content.ReadAsAsync<JArray>();
                    tenants = SetCurrentTenant(tenants);
                    return tenants;
                    //response = Transfer(response);
                    //response.Content = new StringContent(tenants.ToString(), Encoding.UTF8, "application/json");
                    //return response;
                }
            }
            else
            {
                using (var client = GetClient(Utils.GetCSMUrl(Request.RequestUri.Host)))
                {
                    var response = await Utils.Execute(client.GetAsync("tenants" + "?api-version=2014-04-01"));
                    if (!response.IsSuccessStatusCode)
                    {
                        return null;
                    }

                    var tenants = (JArray)(await response.Content.ReadAsAsync<JObject>())["value"];
                    tenants = SetCurrentTenant(ToTenantDetails(tenants));
                    //response = Transfer(response);
                    //response.Content = new StringContent(tenants.ToString(), Encoding.UTF8, "application/json");
                    //return response;
                    return tenants;
                }
            }
        }

        private async Task<HttpResponseMessage> GetTenants(string path)
        {
            var parts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
            {
                if (!Request.RequestUri.IsLoopback)
                {
                    using (var client = GetClient(Request.RequestUri.GetLeftPart(UriPartial.Authority)))
                    {
                        var response = await Utils.Execute(client.GetAsync("tenantdetails"));
                        if (!response.IsSuccessStatusCode)
                        {
                            return response;
                        }

                        var tenants = await response.Content.ReadAsAsync<JArray>();
                        tenants = SetCurrentTenant(tenants);
                        response = Transfer(response);
                        response.Content = new StringContent(tenants.ToString(), Encoding.UTF8, "application/json");
                        return response;
                    }
                }
                else
                {
                    using (var client = GetClient(Utils.GetCSMUrl(Request.RequestUri.Host)))
                    {
                        var response = await Utils.Execute(client.GetAsync(path + "?api-version=2014-04-01"));
                        if (!response.IsSuccessStatusCode)
                        {
                            return response;
                        }

                        var tenants = (JArray)(await response.Content.ReadAsAsync<JObject>())["value"];
                        tenants = SetCurrentTenant(ToTenantDetails(tenants));
                        response = Transfer(response);
                        response.Content = new StringContent(tenants.ToString(), Encoding.UTF8, "application/json");
                        return response;
                    }
                }
            }
            else
            {
                // switch tenant
                var tenantId = Guid.Parse(parts[1]);
                var uri = Request.RequestUri.AbsoluteUri;
                var response = Request.CreateResponse(HttpStatusCode.Redirect);
                response.Headers.Add("Set-Cookie", String.Format("OAuthTenant={0}; path=/; secure; HttpOnly", tenantId));
                response.Headers.Location = new Uri(uri.Substring(0, uri.IndexOf("/api/" + parts[0], StringComparison.OrdinalIgnoreCase)));
                return response;
            }
        }

        private JArray ToTenantDetails(JArray tenants)
        {
            var result = new JArray();
            foreach (var tenant in tenants)
            {
                var value = new JObject();
                value["DisplayName"] = tenant["tenantId"];
                value["DomainName"] = tenant["tenantId"];
                value["TenantId"] = tenant["tenantId"];
                result.Add(value);
            }
            return result;
        }

        private HttpResponseMessage Transfer(HttpResponseMessage response)
        {
            var ellapsed = response.Headers.GetValues(Utils.X_MS_Ellapsed).First();
            response = Request.CreateResponse(response.StatusCode);
            response.Headers.Add(Utils.X_MS_Ellapsed, ellapsed);
            return response;
        }

        private JArray SetCurrentTenant(JArray tenants)
        {
            var tid = (string)GetClaims()["tid"];
            foreach (var tenant in tenants)
            {
                tenant["Current"] = (string)tenant["TenantId"] == tid;
            }
            return tenants;
        }

        private JObject GetClaims()
        {
            var jwtToken = Request.Headers.GetValues(Utils.X_MS_OAUTH_TOKEN).FirstOrDefault();
            var base64 = jwtToken.Split('.')[1];

            // fixup
            int mod4 = base64.Length % 4;
            if (mod4 > 0)
            {
                base64 += new string('=', 4 - mod4);
            }

            var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
            return JObject.Parse(json);
        }

        private async Task<IList<string>> GetSiteLocations(string token, SubscriptionInfo[] subscriptions)
        {
            using (var client = GetRMClient(token, subscriptions.FirstOrDefault().subscriptionId))
            {
                var websites = (await client.Providers.GetAsync("Microsoft.Web")).Provider;
                return websites.ResourceTypes.FirstOrDefault(rt => rt.Name == "sites").Locations;
            }
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