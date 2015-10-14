using System.Net.Http;

namespace Slingshot.Helpers
{
    public class HttpClientFactory
    {
        public static HttpClient Client { get; set; }

        public static HttpClient CreateClient(HttpClientHandler handler = null)
        {
            if (Client != null)
                return Client;

            return new HttpClient(handler: handler);
        }
    }
}