using AzureDeployButton.Concrete;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzureDeployButton.Abstract
{
    public abstract class Repository
    {
        protected Uri _inputUri;
        protected string _repoUrl;
        protected string _branch;

        public Repository(Uri uri)
        {
            _inputUri = uri;
        }

        public abstract string GetCustomTemplate();

        public abstract string RepositoryUrl { get; }

        public abstract string Branch { get; }

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
