using System;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Slingshot.Concrete;
using Slingshot.Helpers;
using Slingshot.Models;

namespace Slingshot.Abstract
{
    public abstract class Repository
    {
        protected string _host;
        protected string _token;

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
        protected SourceControlInfo _scmInfo;

        public Repository(Uri uri, string host, string token)
        {
            _inputUri = uri;
            _host = host;
            _token = token;
        }

        public abstract string ProviderName { get; }

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
                        // remove query string and slash at the end or extra path
                        // e.g https://bitbucket.org/account/myrepo/src?at=mybranch&manual=false
                        //  or https://bitbucket.org/account/myrepo/src/?at=mybranch&manual=false
                        //  or https://github.com/account/myrepo/tree/mybranch
                        //  or https://bitbucket.org/account/myrepo?at=mybranch&manual=false
                        // expecting: myrepo
                        _repositoryName = _inputUri.Segments[2].Split(new char[] { '/', '?' })[0];
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

        public abstract Task<string> GetBranch();

        public abstract Task<JObject> DownloadTemplateAsync();

        public abstract Task<string> GetTemplateUrlAsync();

        public abstract Task<string> GetScmType();

        public virtual Task<bool> IsPrivate()
        {
            return Task.FromResult(false);
        }

        public virtual Task<SourceControlInfo> GetScmInfo()
        {
            return Task.FromResult(default(SourceControlInfo));
        }

        public virtual Task<bool> HasScmInfo()
        {
            return Task.FromResult(false);
        }

        public virtual Task<IPullRequestInfo> GetPullRequest(string prId)
        {
            return Task.FromResult<IPullRequestInfo>((IPullRequestInfo)new object());
        }

        public virtual Task UpdatePullRequest(string prId, dynamic content)
        {
            return Task.FromResult(new object());
        }

        /// <summary>
        /// <para>Pubic repo should always return true, private repo and use`s access token has access to the repo, return true</para>
        /// <para>All other case return false, e.g:</para>
        /// <para>Private repo and use`s access token is not able to access repo, return false</para>
        /// </summary>
        public virtual Task<bool> HasAccess()
        {
            return Task.FromResult(true);
        }

        protected virtual async Task<JObject> DownloadTemplate(string templateUrl)
        {
            JObject template = null;
            using (HttpClient client = Helpers.HttpClientFactory.CreateClient())
            {
                var templateResponse = await client.GetAsync(templateUrl);
                if (templateResponse.IsSuccessStatusCode)
                {
                    template = JObject.Parse(templateResponse.Content.ReadAsStringAsync().Result);
                }
            }

            return template;
        }

        public static Repository CreateRepositoryObj(string url, string host, string token)
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
                return new BitbucketRepository(repositoryUri, host, token);
            }
            else
            {
                throw new NotSupportedException("Invalid git repository.  Currently deployments can only be made from github.com repositories");
            }
        }

        protected static HttpClient CreateHttpClient(string accessToken = null, HttpClientHandler handler = null)
        {
            var client = Helpers.HttpClientFactory.CreateClient(handler);
            client.MaxResponseContentBufferSize = 1024 * 1024 * 10;
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", "AzureDeploy");
            if (!String.IsNullOrEmpty(accessToken))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }
            return client;
        }
    }
}
