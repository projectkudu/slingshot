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
            //config.Routes.MapHttpRoute("get-token", "api/token", new { controller = "LRSARM", action = "GetToken" }, new { verb = new HttpMethodConstraint("GET", "HEAD") });
            
            //config.Routes.MapHttpRoute("get-subscriptions", "api/subscriptions", new { controller = "ARM", action = "Subscriptions" }, new { verb = new HttpMethodConstraint("GET") });
            //config.Routes.MapHttpRoute("get-subscription-appservicenameavailable", "api/subscriptions/{subscriptionId}/sites/{appServiceName}", new { controller = "ARM", action = "IsAppServiceNameAvailable" }, new { verb = new HttpMethodConstraint("GET") });
            config.Routes.MapHttpRoute("get-lrs-template", "api/lrstemplate", new { controller = "LRSARM", action = "GetTemplate" }, new { verb = new HttpMethodConstraint("GET") });
            config.Routes.MapHttpRoute("post-deployments", "api/lrsdeployments/{subscriptionId}", new { controller = "LRSARM", action = "Deploy" }, new { verb = new HttpMethodConstraint("POST") });
            config.Routes.MapHttpRoute("get-deployments-status", "api/lrsdeployments/{subscriptionId}/rg/{resourceGroup}", new { controller = "LRSARM", action = "GetDeploymentStatus" }, new { verb = new HttpMethodConstraint("GET") });
            //config.Routes.MapHttpRoute("get-scmdeployments-status", "api/deployments/{subscriptionId}/rg/{resourceGroup}/scm", new { controller = "ARM", action = "GetScmDeploymentStatus" }, new { verb = new HttpMethodConstraint("GET") });
            //config.Routes.MapHttpRoute("post-deployments-notification", "api/deploymentsnotification", new { controller = "ARM", action = "DeploymentNotification" }, new { verb = new HttpMethodConstraint("POST") });

            config.Routes.MapHttpRoute("get", "api/{*path}", new { controller = "LRSARM", action = "Get" }, new { verb = new HttpMethodConstraint("GET", "HEAD") });

            //config.Routes.MapHttpRoute(
            //    name: "DefaultApi",
            //    routeTemplate: "{controller}/{id}",
            //    defaults: new { id = RouteParameter.Optional }
            //);

            // Uncomment the following line of code to enable query support for actions with an IQueryable or IQueryable<T> return type.
            // To avoid processing unexpected or malicious queries, use the validation settings on QueryableAttribute to validate incoming queries.
            // For more information, visit http://go.microsoft.com/fwlink/?LinkId=279712.
            //config.EnableQuerySupport();

            // To disable tracing in your application, please comment out or remove the following line of code
            // For more information, refer to: http://www.asp.net/web-api
            config.EnableSystemDiagnosticsTracing();

            GlobalConfiguration.Configuration.Formatters.XmlFormatter.SupportedMediaTypes.Clear();
        }
    }
}
