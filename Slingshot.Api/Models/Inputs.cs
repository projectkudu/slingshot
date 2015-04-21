using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Newtonsoft.Json.Linq;

namespace Slingshot.Models
{
    public class DeployInputs
    {
        public JObject parameters { get; set; }
        public string subscriptionId { get; set; }
        public ResourceGroupInfo resourceGroup { get; set; }
        public string templateUrl { get; set; }
    }
}