using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens;
using System.Linq;
using System.Web;
using System.Web.UI.WebControls;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;

namespace Slingshot.Concrete
{
    public static class Telemetry
    {
        private static TelemetryClient sm_client;

        static Telemetry()
        {
            sm_client = new TelemetryClient();
        }

        public static void LogException(Exception e)
        {
            sm_client.TrackException(e);
        }

        public static void LogEvent(string eventName, IDictionary<string,string> properties = null,IDictionary<string, double> metrics = null)
        {
            sm_client.TrackEvent(eventName,properties:properties, metrics:metrics);
        }
    }
}