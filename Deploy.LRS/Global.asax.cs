using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Web;
using System.Web.Http;
//using System.Web.Optimization;
using System.Web.Routing;

namespace Deploy
{
    // Note: For instructions on enabling IIS6 or IIS7 classic mode, 
    // visit http://go.microsoft.com/?LinkId=9394801

    public class WebApiApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            WebApiConfig.Register(GlobalConfiguration.Configuration);
        }
        protected void Application_BeginRequest(Object sender, EventArgs e)
        {
            var context = new HttpContextWrapper(HttpContext.Current);
            GlobalizationManager.SetCurrentCulture(context);

        }

    }
}