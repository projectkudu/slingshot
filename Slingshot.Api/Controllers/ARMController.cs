using Slingshot.Abstract;
using Slingshot.Helpers;
using Slingshot.Models;
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
using Slingshot.Concrete;

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
        #pragma warning disable 4014
        public async Task<HttpResponseMessage> Preview(DeployInputs inputs)
        {
            JObject responseObj = new JObject();
            List<string> providers = new List<string>(32);
            HttpResponseMessage response = null;
            using (var client = GetRMClient(inputs.subscriptionId))
            {
                ResourceGroupCreateOrUpdateResult resourceResult = null;
                string tempRGName = Guid.NewGuid().ToString();

                try
                {
                    resourceResult = await client.ResourceGroups.CreateOrUpdateAsync(
                        tempRGName,
                        new BasicResourceGroup { Location = inputs.resourceGroup.location });

                    // For now we just default to East US for the resource group location.
                    var basicDeployment = new BasicDeployment
                    {
                        Parameters = inputs.parameters.ToString(),
                        TemplateLink = new TemplateLink(new Uri(inputs.templateUrl))
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
                        responseObj["error"] = deploymentResult.Error.Message;
                        response = Request.CreateResponse(deploymentResult.StatusCode, responseObj);
                    }
                }
                finally
                {
                    if (resourceResult != null &&
                        (resourceResult.StatusCode == HttpStatusCode.Created || resourceResult.StatusCode == HttpStatusCode.OK))
                    {
                        string token = GetTokenFromHeader();
                        Task.Run(() => { DeleteResourceGroup(inputs.subscriptionId, token, tempRGName); });
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
                        new BasicResourceGroup { Location = inputs.resourceGroup.location });

                    var templateParams = inputs.parameters.ToString();
                    var basicDeployment = new BasicDeployment
                    {
                        Parameters = templateParams,
                        TemplateLink = new TemplateLink(new Uri(inputs.templateUrl))
                    };

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
        public async Task<HttpResponseMessage> GetDeploymentStatus(string subscriptionId, string resourceGroup, string siteName = null)
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

            if (provisioningState == "Succeeded" && siteName != null)
            {
                using (var wsClient = GetWSClient(subscriptionId))
                {
                    hostName = (await wsClient.WebSites.GetAsync(resourceGroup, siteName, null, null)).WebSite.Properties.HostNames[0];
                    responseObj["siteUrl"] = string.Format("http://{0}", hostName);
                }
            }

            responseObj["provisioningState"] = provisioningState;

            return Request.CreateResponse(HttpStatusCode.OK, responseObj);
        }

        [Authorize]
        [HttpGet]
        public async Task<HttpResponseMessage> GetGitDeploymentStatus(string subscriptionId, string resourceGroup, string siteName)
        {
            HttpResponseMessage response = null;
            using (var client = GetClient())
            {
                string url = string.Format(
                    Constants.CSM.GetGitDeploymentStatusFormat,
                    Utils.GetCSMUrl(Request.RequestUri.Host),
                    subscriptionId,
                    resourceGroup,
                    siteName,
                    Constants.CSM.ApiVersion);

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
                    response = Request.CreateResponse(HttpStatusCode.NotFound, new { error = "Could not find any git deployments" });
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
            JObject returnObj = new JObject();

            Repository repo = Repository.CreateRepositoryObj(repositoryUrl);

            string templateUrl = await repo.GetTemplateUrlAsync();
            JObject template = await repo.DownloadTemplateAsync();
            string branch = await repo.GetBranch();

            if (template != null)
            {
                string token = GetTokenFromHeader();

                var subscriptions = (await Utils.GetSubscriptionsAsync(Request.RequestUri.Host, token))
                                    .Where(s => s.state == "Enabled").ToArray();

                var tenants = await GetTenantsArray();
                var email = GetHeaderValue(Constants.Headers.X_MS_CLIENT_PRINCIPAL_NAME);
                var userDisplayName = GetHeaderValue(Constants.Headers.X_MS_CLIENT_DISPLAY_NAME) ?? email;

                string resourceGroupName = null;

                if (subscriptions.Length >= 1)
                {
                    await GetLocations(template, returnObj, token, subscriptions);
                    resourceGroupName = await GenerateResourceGroupName(token, repo, subscriptions);
                }

                returnObj["subscriptions"] = JArray.FromObject(subscriptions);
                returnObj["tenants"] = tenants;
                returnObj["userDisplayName"] = userDisplayName;
                returnObj["resourceGroupName"] = resourceGroupName;
                returnObj["template"] = template;
                returnObj["templateUrl"] = templateUrl;
                returnObj["repositoryUrl"] = repo.RepositoryUrl;
                returnObj["repositoryDisplayUrl"] = repo.RepositoryDisplayUrl;
                returnObj["branch"] = branch;

                // Check if the template takes in a Website parameter
                if (template["parameters"]["siteName"] != null)
                {
                    // Set the default site name to the same as the rg name
                    returnObj["siteName"] = resourceGroupName;
                }

                response = Request.CreateResponse(HttpStatusCode.OK, returnObj);
            }
            else
            {
                returnObj["error"] = string.Format("Could not find the Azure RM Template '{0}'", repositoryUrl);
                response = Request.CreateResponse(HttpStatusCode.NotFound,returnObj);
            }

            return response;
        }

        private async Task<string> GenerateResourceGroupName(string token, Repository repo, SubscriptionInfo[] subscriptions)
        {
            if (!string.IsNullOrEmpty(repo.RepositoryName))
            {
                bool isAvailable = false;
                var creds = new TokenCloudCredentials(subscriptions.First().subscriptionId, token);
                var rdfeBaseUri = new Uri(Utils.GetRDFEUrl(Request.RequestUri.Host));

                using (var webSiteMgmtClient = CloudContext.Clients.CreateWebSiteManagementClient(creds, rdfeBaseUri))
                {
                    // Make 3 attempts to get a random name (based on the repo name)
                    for (int i = 0; i < 3; i++)
                    {
                        string resourceGroupName = GenerateRandomResourceGroupName(repo.RepositoryName);
                        isAvailable = (await webSiteMgmtClient.WebSites.IsHostnameAvailableAsync(resourceGroupName)).IsAvailable;

                        if (isAvailable)
                        {
                            return resourceGroupName;
                        }
                    }
                }
            }

            return null;
        }

        private string GenerateRandomResourceGroupName(string baseName, int length = 4)
        {
            Random random = new Random();

            var strb = new StringBuilder(baseName.Length + length);
            strb.Append(baseName);
            for (int i = 0; i < length; ++i)
            {
                strb.Append(Constants.Path.HexChars[random.Next(Constants.Path.HexChars.Length)]);
            }

            return strb.ToString();
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

                    var tenantsString = await response.Content.ReadAsStringAsync();
                    var tenants = JArray.Parse(tenantsString);
                    tenants = SetCurrentTenant(tenants);
                    return tenants;
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

                    var tenantsString = await response.Content.ReadAsStringAsync();
                    var tenants = (JArray)(JObject.Parse(tenantsString))["value"];
                    tenants = SetCurrentTenant(ToTenantDetails(tenants));
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
            var ellapsed = response.Headers.GetValues(Constants.Headers.X_MS_Ellapsed).First();
            response = Request.CreateResponse(response.StatusCode);
            response.Headers.Add(Constants.Headers.X_MS_Ellapsed, ellapsed);
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
            IEnumerable<string> dbServerLocations = null;

            using (var client = GetRMClient(token, subscriptions.FirstOrDefault().subscriptionId))
            {
                var websites = (await client.Providers.GetAsync("Microsoft.Web")).Provider;
                locations = websites.ResourceTypes.FirstOrDefault(rt => rt.Name == "sites").Locations
                       .Where(location => location.IndexOf("MSFT", StringComparison.OrdinalIgnoreCase) < 0);

                var parameters = template["parameters"];
                if (parameters != null)
                {
                    var dbServerLocation = parameters["sqlServerLocation"];
                    if (dbServerLocation != null)
                    {
                        var sql = (await client.Providers.GetAsync("Microsoft.Sql")).Provider;
                        dbServerLocations = sql.ResourceTypes.FirstOrDefault(rt => rt.Name == "servers").Locations;
                    }
                }
            }

            returnObj["siteLocations"] = JArray.FromObject(locations);
            returnObj["sqlServerLocations"] = dbServerLocations == null ? null : JArray.FromObject(dbServerLocations);
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
            var creds = new TokenCloudCredentials(subscriptionId, token);
            return new WebSiteManagementClient(creds, new Uri(Utils.GetCSMUrl(Request.RequestUri.Host)));
        }

        private ResourceManagementClient GetRMClient(string token, string subscriptionId)
        {
            var creds = new TokenCloudCredentials(subscriptionId, token);
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
    }
}