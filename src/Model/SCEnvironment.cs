using System;
using Microsoft.Extensions.Configuration;

namespace Model
{
    public class SCEnvironment
    {
        public const int MaxWorkers = 10;
        public const int MaintenancePeriod = 30; // MaintenancePeriod in seconds
        public const string ScPublisherId = "";
        public readonly string PublishersDirName = "_publisher";
        
        public string CertificatesRootPath;
        public string Certificatepass;


        public SCEnvironment(string path, string certPass)
        {
            CertificatesRootPath = path;
            Certificatepass = certPass;
        }

        public bool IsDevelopment()
        {
            return false;
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            return env != null && env.Equals("Development");
        }

        public string GetApnsPkcsCertificatePath(string publisherId, string username, string appId)
        {
            var result = CertificatesRootPath;

            if (publisherId != null && !publisherId.Equals(""))
            {
                result += "//" + PublishersDirName + "//" + publisherId;
            }

            return result + "//" + username + "//" + appId + "_push.p12";
        }
    }
}