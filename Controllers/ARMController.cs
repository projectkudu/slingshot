using AzureDeployButton.Helpers;
using Microsoft.Azure.Management.Resources;
using Microsoft.Azure.Management.Resources.Models;
using Microsoft.WindowsAzure;
using Newtonsoft.Json;
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

        public HttpClient GetClient(string baseUri)
        {
            var client = new HttpClient();
            client.BaseAddress = new Uri(baseUri);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Request.Headers.GetValues("X-MS-OAUTH-TOKEN").FirstOrDefault());
            return client;
        }
    }
}