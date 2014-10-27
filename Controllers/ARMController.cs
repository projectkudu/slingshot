using AzureDeployButton.Helpers;
using AzureDeployButton.Models;
using Microsoft.Azure.Management.Resources;
using Microsoft.Azure.Management.Resources.Models;
using Microsoft.WindowsAzure;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
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
        public async Task<HttpResponseMessage> Get()
        {
            var resourceGroupName = string.Empty;
            var token = Request.Headers.GetValues("X-MS-OAUTH-TOKEN").FirstOrDefault();
            var subscription = string.Empty;

            var subscriptions = await TokenUtils.GetSubscriptionsAsync(TokenUtils.AzureEnvs.Prod, token);

            //var creds = new TokenCloudCredentials(subscription, token);
            //var client = new ResourceManagementClient(creds);

            //var res1 = await client.ResourceGroups.CreateOrUpdateAsync(resourceGroupName, new BasicResourceGroup { Location = "East US" });
            
            //var parameters = new
            //{
            //    siteName = new { value = "TmpBlah678761" },
            //    hostingPlanName = new { value = "TmpBlah67876HP" },
            //    siteLocation = new { value = "East US" }
            //};

            //var basicDeployment = new BasicDeployment
            //{
            //    Parameters = JsonConvert.SerializeObject(parameters),
            //    TemplateLink = new TemplateLink(new Uri("https://dl.dropboxusercontent.com/u/2209341/EmptySite.json"))
            //};
            //var res2 = await client.Deployments.CreateOrUpdateAsync(resourceGroupName, "MyDep", basicDeployment);

            return Request.CreateResponse(HttpStatusCode.OK);
            //IHttpRouteData routeData = Request.GetRouteData();
            //string path = routeData.Values["path"] as string;
            //if (String.IsNullOrEmpty(path))
            //{
            //    var response = Request.CreateResponse(HttpStatusCode.Redirect);
            //    string fullyQualifiedUrl = Request.RequestUri.GetLeftPart(UriPartial.Authority);
            //    response.Headers.Location = new Uri(new Uri(fullyQualifiedUrl), "subscriptions");
            //    return response;
            //}

            //using (var client = GetClient("https://management.azure.com"))
            //{
            //    return await client.GetAsync(path + "?api-version=2014-04-01");
            //}
        }

        [Authorize]
        [HttpGet]
        public HttpResponseMessage Deploy()
        {
            string repositoryUrl = Request.Headers.GetValues("referer").FirstOrDefault();

            var response = Request.CreateResponse(HttpStatusCode.Moved);
            string format = "https://{0}:{1}?repository={2}";
            response.Headers.Location = new Uri(string.Format(format, Request.RequestUri.Host, Request.RequestUri.Port, repositoryUrl));
            return response;
            //return Request.CreateResponse(HttpStatusCode.OK);
        }

        [Authorize]
        [HttpPost]
        public async Task<HttpResponseMessage> DeployTemplate([FromBody] JObject parameters, string subscriptionId, string templateUrl)
        {
            CreateDeploymentResponse responseObj = new CreateDeploymentResponse();
            HttpResponseMessage response = null;

            try
            {
                // etodo: what should we do about this?
                var resourceGroupName = "ehrg01";
                using (var client = GetRMClient(subscriptionId))
                {
                    string location = GetParamOrDefault(parameters, "siteLocation", "East US");

                    var resourceResult = await client.ResourceGroups.CreateOrUpdateAsync(resourceGroupName, new BasicResourceGroup { Location = location });
                    var templateParams = parameters.ToString();
                    var basicDeployment = new BasicDeployment
                    {
                        Parameters = templateParams,
                        TemplateLink = new TemplateLink(new Uri(templateUrl))
                    };

                    var deploymentResult = await client.Deployments.CreateOrUpdateAsync(resourceGroupName, "MyDep", basicDeployment);
                    responseObj.DeploymentUrl = TokenUtils.GetCsmUrl(TokenUtils.AzureEnvs.Prod) + deploymentResult.Deployment.Id + "?api-version=" + TokenUtils.CSMApiVersion;
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
        public async Task<HttpResponseMessage> GetDeploymentStatus(string subscriptionId, string deploymentUrl)
        {
            LongRunningOperationResponse status = null;
            using (var client = GetRMClient(subscriptionId))
            {
                status = await client.GetLongRunningOperationStatusAsync(deploymentUrl);
            }

            return Request.CreateResponse(HttpStatusCode.OK, status);
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
                //repositoryUrl = repositoryUrl.TrimEnd(new char[] { '/' });
                Uri repositoryUri = new Uri(repositoryUrl);
                if (repositoryUri.Segments.Length >= 3)
                {
                    string user = repositoryUri.Segments[1].Trim(new char[] { '/' });
                    string repo = repositoryUri.Segments[2].Trim(new char[] { '/' });
                    templateUrl = string.Format("https://raw.githubusercontent.com/{0}/{1}/master/azuredeploy.json", user, repo);
                    template = await DownloadTemplate(templateUrl);
                }

                //templateUrl = repositoryUrl.TrimEnd(new char[] { '/' }) + "/blob/master/azuredeploy.json";
                //if (repositoryUrl.EndsWith("readme.md", StringComparison.OrdinalIgnoreCase))
                //{
                //    //etodo: I think this probably should be handled on the client side before requesting the template
                //    int lastSlashIndex = repositoryUrl.LastIndexOf('/');
                //    templateUrl = repositoryUrl.Remove(lastSlashIndex) + "/azuredeploy.json";
                //}
                //else
                //{
                    //templateUrl = repositoryUrl.TrimEnd(new char[] { '/' }) + "/blob/master/azuredeploy.json";
                //}
                
                if (template == null)
                {
                    templateUrl = EmptySiteTemplateUrl;
                    template = await DownloadTemplate(templateUrl);
                }
            }

            if (template != null)
            {
                string token = GetTokenFromHeader();
                var subscriptions = await TokenUtils.GetSubscriptionsAsync(TokenUtils.AzureEnvs.Prod, token);

                // etodo: there's gotta be a better way to do this.
                JObject returnObj = new JObject();
                returnObj["template"] = template;
                returnObj["subscriptions"] = JArray.Parse(JsonConvert.SerializeObject(subscriptions));
                response = Request.CreateResponse(HttpStatusCode.OK, returnObj);
                response.Headers.Add("templateUrl", templateUrl);
            }
            else
            {
                response = Request.CreateResponse(HttpStatusCode.NotFound);
            }


            return response;
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