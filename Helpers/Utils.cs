using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Slingshot.Helpers
{
    public class SubscriptionInfo
    {
        public string id { get; set; }
        public string subscriptionId { get; set; }
        public string displayName { get; set; }
        public string state { get; set; }
    }

    public class Utils
    {
        public class ResultOf<T>
        {
            public T[] value { get; set; }
        }

        public enum AzureEnvs
        {
            Next = 0,
            Current = 1,
            Dogfood = 2,
            Prod = 3
        }

        public static async Task<SubscriptionInfo[]> GetSubscriptionsAsync(string host, string token)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var url = string.Format("{0}/subscriptions?api-version={1}", Utils.GetCSMUrl(host), Constants.CSM.ApiVersion);
                using (var response = await client.GetAsync(url))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        var result = await response.Content.ReadAsAsync<ResultOf<SubscriptionInfo>>();
                        return result.value;
                    }

                    var content = await response.Content.ReadAsStringAsync();
                    if (content.StartsWith("{"))
                    {
                        var error = (JObject)JObject.Parse(content)["error"];
                        if (error != null)
                        {
                            throw new InvalidOperationException(String.Format("GetSubscriptions {0}, {1}", response.StatusCode, error.Value<string>("message")));
                        }
                    }

                    throw new InvalidOperationException(String.Format("GetSubscriptions {0}, {1}", response.StatusCode, await response.Content.ReadAsStringAsync()));
                }
            }
        }

        public static async Task<HttpResponseMessage> Execute(Task<HttpResponseMessage> task)
        {
            var watch = new Stopwatch();
            watch.Start();
            var response = await task;
            watch.Stop();
            response.Headers.Add(Constants.Headers.X_MS_Ellapsed, watch.ElapsedMilliseconds + "ms");
            return response;
        }

        public static string GetCSMUrl(string host)
        {
            if (host.EndsWith(".antares-int.windows-int.net", StringComparison.OrdinalIgnoreCase))
            {
                return "https://api-next.resources.windows-int.net";
            }
            else if (host.EndsWith(".antares-test.windows-int.net", StringComparison.OrdinalIgnoreCase))
            {
                return "https://api-current.resources.windows-int.net";
            }
            else if (host.EndsWith(".ant-intapp.windows-int.net", StringComparison.OrdinalIgnoreCase))
            {
                return "https://api-dogfood.resources.windows-int.net";
            }

            return "https://management.azure.com";
        }

        public static string GetRDFEUrl(string host)
        {
            if (host.EndsWith(".antares-int.windows-int.net", StringComparison.OrdinalIgnoreCase))
            {
                return "https://umapinext.rdfetest.dnsdemo4.com";
            }
            else if (host.EndsWith(".antares-test.windows-int.net", StringComparison.OrdinalIgnoreCase))
            {
                return "https://umapi.rdfetest.dnsdemo4.com";
            }
            else if (host.EndsWith(".ant-intapp.windows-int.net", StringComparison.OrdinalIgnoreCase))
            {
                return "https://umapi-preview.core.windows-int.net";
            }

            return "https://management.core.windows.net";
        }
    }
}