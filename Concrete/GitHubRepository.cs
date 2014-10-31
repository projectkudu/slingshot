using AzureDeployButton.Abstract;
using AzureDeployButton.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace AzureDeployButton.Concrete
{
    public class GitHubRepository : Repository
    {
        public GitHubRepository(Uri uri) : base(uri)
        {
        }

        public override string GetCustomTemplate()
        {
            throw new NotImplementedException();
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
    }
}