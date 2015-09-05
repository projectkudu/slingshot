using System;

namespace Slingshot.Models
{
    public class SourceControlInfo
    {
        public string name { get; set; }
        public string token { get; set; }
        public string tokenSecret { get; set; }
        public string refreshToken { get; set; }
        public DateTime? expirationTime { get; set; }
    }
}