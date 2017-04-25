using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;

namespace Deploy.Concrete
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
        public static void LogEvent(string eventName, IDictionary<string, string> properties = null, IDictionary<string, double> metrics = null)
        {
            sm_client.TrackEvent(eventName, properties: properties, metrics: metrics);
        }

    }
}