using System;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Slingshot.Concrete;
using Slingshot.Helpers;

namespace Slingshot.Abstract
{
    public abstract class Repository
    {
        protected Uri _inputUri;
        protected string _repoUrl;
        protected string _repoDisplayUrl;
        protected string _branch;
        protected string _repositoryName;
        protected string _userName;
        protected string _templateUrl;
        protected string _scmType;
        protected JObject _template;
        protected bool? _isPrivate;

        public Repository(Uri uri)
        {
            _inputUri = uri;
        }

        public virtual string RepositoryDisplayUrl
        {
            get
            {
                if (string.IsNullOrEmpty(_repoDisplayUrl))
                {
                    if (_inputUri.Segments.Length > 2)
                    {
                        _repoDisplayUrl = string.Format(
                            CultureInfo.InvariantCulture,
                            "https://{0}{1}{2}{3}",
                            _inputUri.Host,
                            _inputUri.Segments[0],
                            _inputUri.Segments[1],
                            _inputUri.Segments[2].Trim(Constants.Path.SlashChars));
                    }
                    else
                    {
                        throw new ArgumentException("Invalid repository URL");
                    }
                }

                return _repoDisplayUrl;
            }
        }

        public virtual string RepositoryUrl
        {
            get
            {
                if (string.IsNullOrEmpty(_repoUrl))
                {
                    if (_inputUri.Segments.Length > 2)
                    {
                        _repoUrl = string.Format(CultureInfo.InvariantCulture, "https://{0}/{1}/{2}", _inputUri.Host, UserName, RepositoryName);
                    }
                }
                return _repoUrl;
            }
        }

        public virtual string RepositoryName
        {
            get
            {
                if (string.IsNullOrEmpty(_repositoryName))
                {
                    if (_inputUri.Segments.Length > 2)
                    {
                        _repositoryName = _inputUri.Segments[2].Trim(Constants.Path.SlashChars);
                    }
                    else
                    {
                        throw new ArgumentException("Could not parse repository name from repository URL");
                    }
                }

                return _repositoryName;
            }
        }

        public virtual string UserName
        {
            get
            {
                if (string.IsNullOrEmpty(_userName))
                {
                    if (_inputUri.Segments.Length > 1)
                    {
                        _userName = _inputUri.Segments[1].Trim(Constants.Path.SlashChars);
                    }
                    else
                    {
                        throw new ArgumentException("Could not parse user name from repository URL");
                    }
                }

                return _userName;
            }
        }

#pragma warning disable 1998
        public async virtual Task<string> GetBranch()
        {
            return null;
        }

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

#pragma warning disable 1998
        public async virtual Task<string> GetScmType()
        {
            return null;
        }

#pragma warning disable 1998
        public async virtual Task<bool> IsPrivate()
        {
            // Default we assume repo is public repo
            return false;
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
            else if (string.Equals(repositoryUri.Host, "bitbucket.org", StringComparison.OrdinalIgnoreCase))
            {
                return new BitbucketRepository(repositoryUri);
            }
            else
            {
                throw new NotSupportedException("Invalid git repository.  Currently deployments can only be made from github.com repositories");
            }
        }

        protected static HttpClient CreateHttpClient(HttpClientHandler handler = null)
        {
            var client = handler == null ? new HttpClient() : new HttpClient(handler);
            client.DefaultRequestHeaders.Add("User-Agent", "AzureDeploy");
            return client;
        }
    }
}
