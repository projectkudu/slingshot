using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Slingshot.Helpers
{
    public static class HttpClientUtils
    {
        public static async Task<T> GetJsonAsync<T>(this HttpClient client, string url)
        {
            HttpResponseMessage response = await client.GetAsync(url);
            string content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<T>(content);
        }
    }
}
