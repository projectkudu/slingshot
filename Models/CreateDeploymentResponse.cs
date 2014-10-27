using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace AzureDeployButton.Models
{
    public class CreateDeploymentResponse
    {
        [JsonProperty("error")]
        public string Error { get; set; }

        [JsonProperty("errorCode")]
        public string ErrorCode { get; set; }

        [JsonProperty("deploymentUrl")]
        public string DeploymentUrl { get; set; }
    }
}