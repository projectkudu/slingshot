using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Slingshot.Models
{
    public class SubscriptionInfo
    {
        public string id { get; set; }
        public string subscriptionId { get; set; }
        public string displayName { get; set; }
        public string state { get; set; }
        public ResourceGroupInfo[] resourceGroups { get; set; }
    }

    public class ResourceGroupInfo
    {
        //public string id { get; set; }
        public string name { get; set; }
        public string location { get; set; }
    }
}