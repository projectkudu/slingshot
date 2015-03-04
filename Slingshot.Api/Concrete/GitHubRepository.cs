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
using System.Text;

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
                StringBuilder builder = null;

                if (_inputUri.Segments.Length > 2)
                {
                    string branch = await GetBranch();
                    builder = new StringBuilder(string.Format(Constants.Repository.GitCustomTemplateFolderUrlFormat,
                                                        UserName,
                                                        RepositoryName,
                                                        branch));

                    for (var i = 5; i < _inputUri.Segments.Length; i++)
                    {
                        string segment = _inputUri.Segments[i];
                        if(segment.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                        {
                            break;
                        }

                        builder.Append(segment);
                    }

                    templateUrl = builder.ToString().TrimEnd(Constants.Path.SlashChars) + "/azuredeploy.json";
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

        public override string RepositoryDisplayUrl
        {
            get
            {
                if (string.IsNullOrEmpty(_repoDisplayUrl))
                {
                    string lastSegment = _inputUri.Segments[_inputUri.Segments.Length - 1];
                    string url = _inputUri.ToString();
                    if (lastSegment.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                    {
                        int lastSlashIndex = url.LastIndexOf('/');
                        _repoDisplayUrl = _inputUri.ToString().Substring(0, lastSlashIndex);
                    }
                    else
                    {
                        _repoDisplayUrl = url;
                    }
                }

                return _repoDisplayUrl;
            }
        }

        public override async Task<string> GetBranch()
        {
            if (string.IsNullOrEmpty(_branch))
            {
                if (_inputUri.Segments.Length > 4)
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