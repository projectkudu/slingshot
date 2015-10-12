using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using System.Web.Routing;

namespace Slingshot
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            config.Routes.MapHttpRoute("get-token", "api/token", new { controller = "ARM", action = "GetToken" }, new { verb = new HttpMethodConstraint("GET", "HEAD") });
            
            //config.Routes.MapHttpRoute("get-subscriptions", "api/subscriptions", new { controller = "ARM", action = "Subscriptions" }, new { verb = new HttpMethodConstraint("GET") });
            config.Routes.MapHttpRoute("get-subscription-sitenameavailable", "api/subscriptions/{subscriptionId}/sites/{siteName}", new { controller = "ARM", action = "IsSiteNameAvailable" }, new { verb = new HttpMethodConstraint("GET") });
            config.Routes.MapHttpRoute("get-template", "api/template", new { controller = "ARM", action = "GetTemplate" }, new { verb = new HttpMethodConstraint("GET") });
            config.Routes.MapHttpRoute("post-preview", "api/preview/{subscriptionId}", new { controller = "ARM", action = "Preview" }, new { verb = new HttpMethodConstraint("POST") });
            config.Routes.MapHttpRoute("post-deployments", "api/deployments/{subscriptionId}", new { controller = "ARM", action = "Deploy" }, new { verb = new HttpMethodConstraint("POST") });
            config.Routes.MapHttpRoute("get-deployments-status", "api/deployments/{subscriptionId}/rg/{resourceGroup}", new { controller = "ARM", action = "GetDeploymentStatus" }, new { verb = new HttpMethodConstraint("GET") });
            config.Routes.MapHttpRoute("get-scmdeployments-status", "api/deployments/{subscriptionId}/rg/{resourceGroup}/scm", new { controller = "ARM", action = "GetScmDeploymentStatus" }, new { verb = new HttpMethodConstraint("GET") });
            config.Routes.MapHttpRoute("post-deployments-notification", "api/deploymentsnotification", new { controller = "ARM", action = "DeploymentNotification" }, new { verb = new HttpMethodConstraint("POST") });

            config.Routes.MapHttpRoute("get", "api/{*path}", new { controller = "ARM", action = "Get" }, new { verb = new HttpMethodConstraint("GET", "HEAD") });

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
