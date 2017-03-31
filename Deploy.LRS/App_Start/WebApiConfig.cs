using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using System.Web.Routing;

namespace Deploy
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            config.Routes.MapHttpRoute("get-lrs-template", "api/lrstemplate", new { controller = "LRSARM", action = "GetTemplate" }, new { verb = new HttpMethodConstraint("GET") });
            config.Routes.MapHttpRoute("post-deployments", "api/lrsdeployments/{subscriptionId}", new { controller = "LRSARM", action = "Deploy" }, new { verb = new HttpMethodConstraint("POST") });
            config.Routes.MapHttpRoute("get-deployments-status", "api/lrsdeployments/{subscriptionId}/rg/{resourceGroup}", new { controller = "LRSARM", action = "GetDeploymentStatus" }, new { verb = new HttpMethodConstraint("GET") });
            config.Routes.MapHttpRoute("get", "api/{*path}", new { controller = "LRSARM", action = "Get" }, new { verb = new HttpMethodConstraint("GET", "HEAD") });

            config.EnableSystemDiagnosticsTracing();

            GlobalConfiguration.Configuration.Formatters.XmlFormatter.SupportedMediaTypes.Clear();
        }
    }
}
