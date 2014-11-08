using Slingshot.Abstract;
using Slingshot.Helpers;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.IO;

namespace Slingshot.Concrete
{
    public class GitHubRepository : Repository
    {
        public GitHubRepository(Uri uri) : base(uri)
        {
        }

        public override async Task<JObject> DownloadTemplateAsync()
        {
            if (_template == null)
            {
                await GetTemplateUrlAsync();
            }

            return _template;
        }

        public override async Task<string> GetTemplateUrlAsync()
        {
            if (string.IsNullOrEmpty(_templateUrl))
            {
                JObject template = null;
                string templateUrl = null;

                if (_inputUri.Segments.Length > 2)
                {
                    string branch = await GetBranch();
                    templateUrl = string.Format(Constants.Repository.GitCustomTemplateFormat,
                                                        UserName,
                                                        RepositoryName,
                                                        branch);

                    template = await DownloadTemplate(templateUrl);
                }

                if (template == null)
                {
                    templateUrl = Constants.Repository.EmptySiteTemplateUrl;
                    template = await DownloadTemplate(templateUrl);
                }

                _template = template;
                _templateUrl = templateUrl;
            }

            return _templateUrl;
        }

        public override string RepositoryUrl
        {
            get
            {
                if (string.IsNullOrEmpty(_repoUrl))
                {
                    if (_inputUri.Segments.Length > 2)
                    {
                        _repoUrl = string.Format("https://{0}/{1}/{2}", _inputUri.Host, UserName, RepositoryName);
                    }
                }

                return _repoUrl;
            }
        }

        public override async Task<string> GetBranch()
        {
            if (string.IsNullOrEmpty(_branch))
            {
                if (_inputUri.Segments.Length >= 4)
                {
                    _branch = _inputUri.Segments[4].Trim(Constants.Path.SlashChars);
                }
                else
                {
                    // If the branch isn't in the URL, then we need to look up the default branch
                    // by querying the GitHub API.
                    using (HttpClient client = new HttpClient())
                    {   
                        var url = string.Format(Constants.Repository.GitHubApiRepoInfoFormat, UserName, RepositoryName);
                        client.DefaultRequestHeaders.Add("User-Agent", "AzureDeploy");

                        var content = await client.GetStringAsync(url);
                        var responseObj = JObject.Parse(content);
                        var defaultBranch = responseObj["default_branch"];
                        if (defaultBranch == null)
                        {
                            throw new ArgumentException("Could not discover default branch from repository");
                        }

                        _branch = defaultBranch.Value<string>();
                    }
                }

            }

            return _branch;
        }

        public override string RepositoryName
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

        public override string UserName
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
    }
}