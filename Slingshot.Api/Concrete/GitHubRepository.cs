using System;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Slingshot.Abstract;
using Slingshot.Helpers;

namespace Slingshot.Concrete
{
    public class GitHubRepository : Repository
    {
        public override string ProviderName
        {
            get { return "Github"; }
        }

        public GitHubRepository(Uri uri)
            : base(uri, null, null)
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
                        if (segment.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                        {
                            break;
                        }

                        builder.Append(segment);
                    }

                    templateUrl = builder.ToString().TrimEnd(Constants.Path.SlashChars) + "/azuredeploy.json";
                    template = await DownloadJson(templateUrl);

                    string paramTemplatePath = this.GetParameterTemplatePath();
                    if (template != null && paramTemplatePath != null)
                    {
                        string paramTemplateFullPath = paramTemplatePath;

                        if (!paramTemplatePath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                        {
                            // it is relative path, construct url to point to raw content
                            paramTemplateFullPath = string.Format(
                                CultureInfo.InvariantCulture,
                                "{0}/{1}", builder.ToString().TrimEnd(Constants.Path.SlashChars),
                                paramTemplatePath.Trim(Constants.Path.SlashChars));
                        }

                        JObject paramTemplate = await DownloadJson(paramTemplateFullPath);
                        MergeParametersIntoTemplate(template, paramTemplate);
                    }
                }

                if (template == null)
                {
                    templateUrl = Constants.Repository.EmptySiteTemplateUrl;
                    template = await DownloadJson(templateUrl);
                }

                _template = template;
                _templateUrl = templateUrl;
            }

            return _templateUrl;
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
                    using (HttpClient client = CreateHttpClient())
                    {
                        var url = string.Format(Constants.Repository.GitHubApiRepoInfoFormat, UserName, RepositoryName);

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

#pragma warning disable 1998
        public override async Task<string> GetScmType()
        {
            if (string.IsNullOrWhiteSpace(_scmType))
            {
                _scmType = "git";
            }

            return _scmType;
        }
    }
}