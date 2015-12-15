using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json.Linq;
using Slingshot.Abstract;
using Slingshot.Helpers;
using Slingshot.Models;

namespace Slingshot.Concrete
{
    public class BitbucketRepository : Repository
    {
        public override string ProviderName
        {
            get { return "Bitbucket"; }
        }

        public BitbucketRepository(Uri uri, string host, string token)
            : base(uri, host, token)
        {
        }

        public override async Task<JObject> DownloadTemplateAsync()
        {
            if (_template == null)
            {
                string templateUrl = await this.GetTemplateUrlAsync();
                if (string.Equals(Constants.Repository.EmptySiteTemplateUrl, templateUrl))
                {
                    _template = await this.DownloadJson(await GetTemplateUrlAsync());
                }
                else
                {
                    _template = JObject.Parse(await this.DownloadFile(templateUrl));

                    string paramTemplatePath = this.GetParameterTemplatePath();
                    if (_template != null && paramTemplatePath != null)
                    {
                        JObject paramTemplateJson = null;
                        if (paramTemplatePath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                        {
                            // if it is a URL, assume it is public, otherwise let it failed
                            paramTemplateJson = await this.DownloadJson(paramTemplatePath);
                        }
                        else
                        {
                            // if it is a relative path, download use API
                            paramTemplateJson = JObject.Parse(await this.DownloadFile(paramTemplatePath));
                        }

                        MergeParametersIntoTemplate(_template, paramTemplateJson);
                    }
                }
            }

            return _template;
        }

        public override async Task<string> GetTemplateUrlAsync()
        {
            if (!string.IsNullOrEmpty(_templateUrl))
                return _templateUrl;

            // try to check if user has custom tempate, if they do use it, otherwise use default template
            //
            // * if user has token, we will use token to read repo regardless user has Admin access or not 
            //   if user doesn`t has admin access and try to setup CI, we will let Azure to fail
            //
            // * if it is a public repo, we can access repo thru api regardless user has token or not
            try
            {
                string branch = await this.GetBranch();
                // will take care of both public repo or private repo that needs access token
                if (await this.HasFile(Constants.Repository.CustomTemplateFileName))
                {
                    // indicate we are getting from user`s repo
                    // regarding to how, "DownloadFile" method will find the best way to reach that file (via web link or thru API)
                    _templateUrl = Constants.Repository.CustomTemplateFileName;
                }
                else
                {
                    _templateUrl = Constants.Repository.EmptySiteTemplateUrl;
                }

                return _templateUrl;
            }
            catch (Exception ex)
            {
                // means repo is private and use do not have access to it
                throw new InvalidOperationException("Failed to discover deployment template: " + ex.Message);
            }
        }

        public override async Task<string> GetBranch()
        {
            if (!string.IsNullOrWhiteSpace(_branch))
                return _branch;

            // 1) Get from query string, this is where are the most common case should be when user was auto direct from bitbucket.org
            if (!string.IsNullOrWhiteSpace(_inputUri.Query))
            {
                var queryStrings = HttpUtility.ParseQueryString(_inputUri.Query);
                if (queryStrings["at"] != null)
                {
                    _branch = queryStrings["at"];
                    return _branch;
                }

                if (queryStrings["pr"] != null)
                {
                    string pullRequestId = queryStrings["pr"];
                    var pr = await this.GetPullRequest(pullRequestId);
                    _branch = pr.SourceBranch;
                    return _branch;
                }
            }

            // 2) there will be case that people hand craft the link and without branch information
            try
            {
                // if we have token, try to query from API
                // and we take the "main" branch as default branch
                using (HttpClient client = await CreateHttpClient())
                {
                    var mainBranchUrl = string.Format(CultureInfo.InvariantCulture, Constants.Repository.BitbucketApiMainBranchInfoFormat, UserName, RepositoryName);
                    string content = await client.GetStringAsync(mainBranchUrl);
                    var branchInfo = JObject.Parse(content);
                    _branch = branchInfo.Value<string>("name");
                    return _branch;
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to discover branch information. Or you can specify with query string 'at=[value]'. " + ex.Message);
            }
        }

        public override async Task<string> GetScmType()
        {
            if (!string.IsNullOrWhiteSpace(_scmType))
                return _scmType;

            // try to query API to get scm type regardless it is public or private
            try
            {
                using (HttpClient client = await CreateHttpClient())
                {
                    var repoInfoUrl = string.Format(CultureInfo.InvariantCulture, Constants.Repository.BitbucketApiRepoInfoFormat, UserName, RepositoryName);
                    string content = await client.GetStringAsync(repoInfoUrl);
                    var repoInfoContent = JObject.Parse(content);
                    var scm = repoInfoContent["scm"];
                    if (scm == null)
                    {
                        throw new ArgumentException("Could not discover repository type");
                    }

                    _scmType = scm.Value<string>().ToLowerInvariant();
                    return _scmType;
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to discover repository type, please make sure you have read access to repository. " + ex.Message);
            }
        }

        public override async Task<bool> IsPrivate()
        {
            if (!_isPrivate.HasValue)
            {
                var repoInfoUrl = string.Format(CultureInfo.InvariantCulture, Constants.Repository.BitbucketWebRepoInfoFormat, UserName, RepositoryName);
                var handler = new HttpClientHandler() { AllowAutoRedirect = false };
                using (HttpClient client = CreateHttpClient(handler: handler))
                using (HttpResponseMessage response = await client.GetAsync(repoInfoUrl))
                {
                    _isPrivate = response.StatusCode != HttpStatusCode.OK;
                }
            }

            return _isPrivate.Value;
        }

        public override async Task<SourceControlInfo> GetScmInfo()
        {
            if (_scmInfo == null)
            {
                _scmInfo = await Utils.GetSourceControlAsync(_host, _token, "Bitbucket");
            }

            return _scmInfo;
        }

        public override async Task<bool> HasScmInfo()
        {
            SourceControlInfo info = await this.GetScmInfo();
            return !string.IsNullOrWhiteSpace(info.token) && !string.IsNullOrWhiteSpace(info.refreshToken);
        }

        public override async Task<bool> HasAccess()
        {
            // public
            if (await this.IsPrivate() == false)
            {
                return true;
            }

            // has token and see if able to access repo
            // TODO: check admin access, and fail ealier(notify user when they want to setup CI)
            if (await this.HasScmInfo())
            {
                string repoUrl = string.Format(CultureInfo.InvariantCulture, Constants.Repository.BitbucketApiRepoInfoFormat, UserName, RepositoryName);
                using (HttpClient client = await CreateHttpClient())
                using (HttpResponseMessage response = await client.GetAsync(repoUrl))
                {
                    return response.StatusCode == HttpStatusCode.OK;
                }
            }

            return false;
        }

        public override async Task<IPullRequestInfo> GetPullRequest(string prId)
        {
            try
            {
                using (HttpClient client = await CreateHttpClient())
                {
                    var requestUrl = string.Format(CultureInfo.InvariantCulture, Constants.Repository.BitbucketApiPullRequestInfoFormat, UserName, RepositoryName, prId);
                    dynamic rawObj = await client.GetJsonAsync<dynamic>(requestUrl);
                    return new BitBucketPullRequest(rawObj);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(string.Format("Failed to retrieve information for Pull Request: {0}. {1}", prId, ex.Message));
            }
        }

        public override async Task UpdatePullRequest(string prId, IPullRequestInfo prInfo)
        {
            try
            {
                using (HttpClient client = await CreateHttpClient())
                {
                    var requestUrl = string.Format(CultureInfo.InvariantCulture, Constants.Repository.BitbucketApiPullRequestInfoFormat, UserName, RepositoryName, prId);
                    await client.PutAsJsonAsync<object>(requestUrl, ((BitBucketPullRequest)prInfo).RawContent);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(string.Format("Failed to update Pull Request: {0}. {1}", prId, ex.Message));
            }
        }

        /// <summary>
        /// Caller need to make sure there is access token when run against private repo, otherwise error will be thrown
        /// </summary>
        private async Task<bool> HasFile(string path)
        {
            bool fileExisted = false;
            await GetFileReference(path, async (response) =>
            {
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    fileExisted = true;
                }
                else if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    fileExisted = false;
                }
                else
                {
                    throw new InvalidOperationException(await response.Content.ReadAsStringAsync());
                }
            });

            return fileExisted;
        }

        private async Task<string> DownloadFile(string path)
        {
            string content = null;
            await GetFileReference(path, async (response) =>
            {
                if (response.IsSuccessStatusCode)
                {
                    content = await response.Content.ReadAsStringAsync();
                }
                else
                {
                    throw new InvalidOperationException(await response.Content.ReadAsStringAsync());
                }
            });

            return content;
        }

        /// <summary>
        /// If repo is public, will try to download via web link instead of calling API
        /// </summary>
        private async Task GetFileReference(string path, Func<HttpResponseMessage, Task> actionWhenSuccess)
        {
            string branch = await this.GetBranch();
            // use web link, assume it is public by default to avoid calling API too often (would get block)
            string fileUrl = string.Format(
                CultureInfo.InvariantCulture, 
                Constants.Repository.BitbucketRawFileWebFormat,
                UserName, 
                RepositoryName, 
                branch, 
                path.Trim(Constants.Path.SlashChars));  // trim slash to remove slash at the begining
            string token = null;

            // if it is public, try to use repo access token thru API
            if (await this.IsPrivate())
            {
                SourceControlInfo scmInfo = await this.GetScmInfo();
                token = scmInfo.token;
                fileUrl = string.Format(CultureInfo.InvariantCulture, Constants.Repository.BitbucketApiRawFile, UserName, RepositoryName, branch, path);
            }

            using (HttpClient client = CreateHttpClient(accessToken: token))
            using (HttpResponseMessage response = await client.GetAsync(fileUrl))
            {
                await actionWhenSuccess(response);
            }
        }

        private async Task<HttpClient> CreateHttpClient()
        {
            SourceControlInfo scmInfo = await this.GetScmInfo();
            // if user doesn`t have a token, "scmInfo.token" will be null. In this case HttpClient will be created without token
            return CreateHttpClient(accessToken: scmInfo.token);
        }
    }

    /// <summary>
    /// Partial object, full reponse please reference
    /// https://confluence.atlassian.com/bitbucket/pullrequests-resource-423626332.html#pullrequestsResource-GETaspecificpullrequest
    /// </summary>
    public class BitBucketPullRequest : IPullRequestInfo
    {
        private dynamic _rawContent { get; set; }

        public BitBucketPullRequest(dynamic rawContent)
        {
            _rawContent = rawContent;
        }

        public string SourceBranch
        {
            get
            {
                try
                {
                    return _rawContent.source.branch.name;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Failed to read source branch name from pull request. " + ex.Message);
                }
            }
        }

        public object RawContent
        {
            get { return _rawContent; }
        }

        public void AppendNewLineToDescription(string text)
        {
            try
            {
                string content = _rawContent.description;
                content = content.TrimEnd();
                // double new line is the "new line" markup in Bitbucket
                _rawContent.description = string.Format(CultureInfo.InvariantCulture, "{0}\n\n{1}\n\n", content, text);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to update description of a pull request. " + ex.Message);
            }
        }
    }
}
