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
                    string user = _inputUri.Segments[1].Trim(Constants.Path.SlashChars);
                    string repo = _inputUri.Segments[2].Trim(Constants.Path.SlashChars);
                    templateUrl = string.Format(Constants.Repository.GitCustomTemplateFormat,
                                                        user,
                                                        repo,
                                                        Branch);

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
                        string user = _inputUri.Segments[1].Trim(Constants.Path.SlashChars);
                        string repo = _inputUri.Segments[2].Trim(Constants.Path.SlashChars);
                        _repoUrl = string.Format("https://{0}/{1}/{2}", _inputUri.Host, user, repo);
                    }
                }

                return _repoUrl;
            }
        }

        public override string Branch
        {
            get 
            {
                if (string.IsNullOrEmpty(_branch))
                {
                    if (_inputUri.Segments.Length >= 4)
                    {
                        _branch = _inputUri.Segments[4].Trim(Constants.Path.SlashChars);
                    }
                    else
                    {
                        _branch = "master";
                    }

                }

                return _branch;
            }
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
                }

                return _repositoryName;
            }
        }
    }
}