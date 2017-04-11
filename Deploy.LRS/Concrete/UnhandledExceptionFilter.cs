using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http.Filters;

namespace Deploy.Concrete
{
    public class UnhandledExceptionFilter : ExceptionFilterAttribute
    {
        public override void OnException(HttpActionExecutedContext context)
        {
            Telemetry.LogException(context.Exception);
            JObject responseObj = new JObject();
            responseObj["error"] = context.Exception.Message;
            responseObj["exception"] = context.Exception.ToString();
            context.Response = context.Request.CreateResponse(HttpStatusCode.InternalServerError, responseObj);
        }
    }
}