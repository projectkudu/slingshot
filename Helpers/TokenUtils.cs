using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web;

namespace Slingshot.Helpers
{
    public static class TokenUtils
    {
        public const string CSMApiVersion = "2014-01-01";

        private static string[] CSMUrls = new[]
        {
            "https://api-next.resources.windows-int.net",
            "https://api-current.resources.windows-int.net",
            "https://api-dogfood.resources.windows-int.net",
            "https://management.azure.com"
        };

        public static async Task<SubscriptionInfo[]> GetSubscriptionsAsync(AzureEnvs env, string token)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var url = string.Format("{0}/subscriptions?api-version={1}", CSMUrls[(int)env], CSMApiVersion);
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

        public static string GetCsmUrl(AzureEnvs env)
        {
            return CSMUrls[(int)env];
        }

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
    }

    public class SubscriptionInfo
    {
        public string id { get; set; }
        public string subscriptionId { get; set; }
        public string displayName { get; set; }
        public string state { get; set; }
    }

}