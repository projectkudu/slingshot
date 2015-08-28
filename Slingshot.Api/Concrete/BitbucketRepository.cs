using System;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json.Linq;
using Slingshot.Abstract;
using Slingshot.Helpers;

namespace Slingshot.Concrete
{
    public class BitbucketRepository : Repository
    {
        public BitbucketRepository(Uri uri)
            : base(uri)
        {
        }

        public override async Task<JObject> DownloadTemplateAsync()
        {
            if (_template == null)
            {
                _template = await this.DownloadTemplate(await GetTemplateUrlAsync());
            }

            return _template;
        }

        public override async Task<string> GetTemplateUrlAsync()
        {
            if (string.IsNullOrEmpty(_templateUrl))
            {
                string branch = await GetBranch();

                using (HttpClient client = CreateHttpClient())
                {
                    var url = string.Format(Constants.Repository.BitBucketApiCommitsInfoFormat, UserName, RepositoryName, branch);
                    string content = await client.GetStringAsync(url);
                    var responseObj = JObject.Parse(content);
                    string latestCommit = responseObj["values"][0].Value<string>("hash");
                    _templateUrl = string.Format(Constants.Repository.BitbucketArmTemplateFormat, UserName, RepositoryName, latestCommit);
                    _template = await this.DownloadTemplate(_templateUrl);
                    if (_template == null)
                    {
                        _templateUrl = Constants.Repository.EmptySiteTemplateUrl;
                    }
                }
            }

            return _templateUrl;
        }

        public override async Task<string> GetBranch()
        {
            if (string.IsNullOrWhiteSpace(_branch))
            {
                if (!string.IsNullOrWhiteSpace(_inputUri.Query))
                {
                    var queryStrings = HttpUtility.ParseQueryString(_inputUri.Query);
                    if (queryStrings["at"] != null)
                    {
                        _branch = queryStrings["at"];
                        return _branch;
                    }
                }

                using (HttpClient client = CreateHttpClient())
                {
                    var mainBranchUrl = string.Format(CultureInfo.InvariantCulture, Constants.Repository.BitbucketApiMainBranchInfoFormat, UserName, RepositoryName);
                    string content = await client.GetStringAsync(mainBranchUrl);
                    var branchInfo = JObject.Parse(content);
                    _branch = branchInfo.Value<string>("name");
                }
            }

            return _branch;
        }

#pragma warning disable 1998
        public override async Task<string> GetScmType()
        {
            if (string.IsNullOrWhiteSpace(_scmType))
            {
                using (HttpClient client = CreateHttpClient())
                {
                    var repoInfoUrl = string.Format(CultureInfo.InvariantCulture, Constants.Repository.BitbucketApiRepoInfoFormat, UserName, RepositoryName);
                    string content = await client.GetStringAsync(repoInfoUrl);
                    var repoInfoContent = JObject.Parse(content);
                    var scm = repoInfoContent["scm"];
                    if (scm == null)
                    {
                        throw new ArgumentException("Could not discover default branch from repository");
                    }

                    _scmType = scm.Value<string>().ToLowerInvariant();
                }
            }

            return _scmType;
        }
    }
}
