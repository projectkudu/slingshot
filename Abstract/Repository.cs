using AzureDeployButton.Concrete;
using AzureDeployButton.Helpers;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace AzureDeployButton.Abstract
{
    public abstract class Repository
    {
        protected Uri _inputUri;
        protected string _repoUrl;
        protected string _branch;
        protected string _templateUrl;
        protected JObject _template;

        public Repository(Uri uri)
        {
            _inputUri = uri;
        }

        public abstract string RepositoryUrl { get; }

        public abstract string Branch { get; }

        #pragma warning disable 1998
        public async virtual Task<JObject> DownloadTemplateAsync()
        {
            return null;
        }

        #pragma warning disable 1998
        public async virtual Task<string> GetTemplateUrlAsync()
        {
            return null;
        }

        protected virtual async Task<JObject> DownloadTemplate(string templateUrl)
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

        public static Repository CreateRepositoryObj(string url)
        {
            Uri repositoryUri = null;
            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentNullException();
            }

            repositoryUri = new Uri(url);

            if (string.Equals(repositoryUri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
            {
                return new GitHubRepository(repositoryUri);
            }
            else
            {
                return null;
            }
        }
    }
}
