using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Model;
using Model.APNS;
using Model.Utils;
using Newtonsoft.Json.Linq;

namespace ApplicationCore.APNS
{
    public interface IApnHelper
    {
        X509Certificate2 GetClientCertificate(string publisherId, string username, string appId);
        JObject GenerateJsonObjectApnPayload(Dictionary<string, string> payloadData);
        ApnPayload GenerateApnPayload(Dictionary<string, string> payloadData);

        Task<bool> SaveUserCertFile(string publisherId, string username, string appId, Stream stream);
    }

    public class ApnHelper : IApnHelper
    {
        public const string ERROR_APN_CERTIFICATE_MISSING = "APNs Certificate is missing";
        public const string ERROR_APN_CERTIFICATE_EXPIRED = "APNs Certificate has expired";

        private readonly SCEnvironment _scEnvironment;

        public ApnHelper(SCEnvironment scEnvironment)
        {
            _scEnvironment = scEnvironment;
        }

        public X509Certificate2 GetClientCertificate(string publisherId, string username, string appId)
        {
            var certificatePath = _scEnvironment.GetApnsPkcsCertificatePath(publisherId, username, appId);
            if (!File.Exists(certificatePath))
            {
                throw new ApnConfigurationException(ERROR_APN_CERTIFICATE_MISSING);
            }

            var certificatePassword = _scEnvironment.Certificatepass;

            X509Certificate2 clientCertificate;
            try
            {
                clientCertificate = new X509Certificate2(certificatePath, certificatePassword,
                    X509KeyStorageFlags.MachineKeySet);
            }
            catch (CryptographicException ex)
            {
                //  _logger.LogError<ApnHelper>(ex, ex.Message, nameof(GetClientCertificate), Apn);
                throw new ApnConfigurationException($"[{ex.Source}] {ex.Message}");
            }

            if (clientCertificate.NotAfter < DateTime.Now)
            {
                throw new ApnConfigurationException(ERROR_APN_CERTIFICATE_EXPIRED);
            }

            return clientCertificate;
        }

        public JObject GenerateJsonObjectApnPayload(Dictionary<string, string> payloadData)
        {
            return JsonUtil.Deserialize<JObject>(GenerateApnPayload(payloadData).GeneratePayload());
        }

        public ApnPayload GenerateApnPayload(Dictionary<string, string> payloadData)
        {
            ApnPayload apnPayload = new ApnPayload();

            Dictionary<string, string> data = new Dictionary<string, string>(payloadData);

            if (data.ContainsKey("alert"))
            {
                string alert = data["alert"];

                // 20 is minimum number of bytes per payload with alert: {"aps":{"alert":""}}
                if (alert.Length > ApnPayload.MAX_PAYLOAD_SIZE - 23)
                {
                    alert = alert.Substring(0, ApnPayload.MAX_PAYLOAD_SIZE - 23) + "...";
                }

                apnPayload.AddAlert(null, alert);
                data.Remove("alert");
            }
            else if (data.ContainsKey("alertTitle") && data.ContainsKey("alertBody"))
            {
                string alertTitle = data["alertTitle"];
                string alertBody = data["alertBody"];

                apnPayload.AddAlert(alertTitle, alertBody);
            }

            if (data.ContainsKey("badge"))
            {
                string badge = data["badge"];

                apnPayload.AddBadge(StringUtil.ParseInt(badge, 1));
                data.Remove("badge");
            }

            if (data.ContainsKey("sound"))
            {
                string sound = data["sound"].Trim();

                if (!string.IsNullOrEmpty(sound))
                {
                    apnPayload.AddSound(sound);
                }

                data.Remove("sound");
            }

            if (data.ContainsKey("content-available"))
            {
                apnPayload.AddContentAvailable();
                data.Remove("content-available");
            }

            //extract action
            if (payloadData.ContainsKey("action") && payloadData["action"] != "")
            {
                string action = data["action"].Trim();

                if (!string.IsNullOrEmpty(action))
                {
                    apnPayload.AddCustomProperty("action", action, true);
                }

                data.Remove("action");
            }

            if (payloadData.ContainsKey("external_action") && payloadData["external_action"] != "")
            {
                string external_action = data["external_action"].Trim();

                if (!string.IsNullOrEmpty(external_action))
                {
                    apnPayload.AddCustomProperty("external_action", external_action, true);
                }

                data.Remove("external_action");
            }

            string externalImageUrl = string.Empty;
            string localImageName = string.Empty;

            if (data.ContainsKey("external_img"))
            {
                externalImageUrl = data["external_img"].Trim();
            }

            if (data.ContainsKey("local_img"))
            {
                localImageName = data["local_img"].Trim();
            }

            if (!string.IsNullOrEmpty(externalImageUrl) || !string.IsNullOrEmpty(localImageName) ||
                data.ContainsKey("buttons"))
            {
                apnPayload.AddMutableContent();

                if (!string.IsNullOrEmpty(externalImageUrl) || !string.IsNullOrEmpty(localImageName))
                {
                    if (!string.IsNullOrEmpty(externalImageUrl))
                    {
                        apnPayload.AddCustomProperty("external_img", externalImageUrl);
                    }
                    else //localImageName is not empty
                    {
                        apnPayload.AddCustomProperty("local_img", localImageName);
                    }
                }

                if (data.ContainsKey("buttons"))
                {
                    //Generate a random string for notification, length: 10
                    string randomGeneratedCategory = CryptoUtil.GetRandomAlphanumericString(10);

                    apnPayload.AddCategory(randomGeneratedCategory);

                    string buttons = data["buttons"];
                    JArray jsonArray = JsonUtil.Deserialize<JArray>(buttons);

                    apnPayload.AddCustomProperty("buttons", jsonArray);
                }
            }

            string module = data.ContainsKey("module") ? data["module"] : null;
            string type = data.ContainsKey("type") ? data["type"] : null;

            // Add custom parameters only for custom modules or standard module but not default type
            if (module != null || type != Message.TYPE_DEFAULT)
            {
                JObject scm = new JObject();
                foreach (KeyValuePair<string, string> kvp in data)
                {
                    scm.Add(kvp.Key, kvp.Value);
                }

                apnPayload.AddCustomProperty("scm", scm);
            }

            return apnPayload;
        }

        public async Task<bool> SaveUserCertFile(string publisherId, string username, string appId, Stream stream)
        {
            var path = _scEnvironment.GetApnsPkcsCertificatePath(publisherId, username, appId);

            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            var dir = Path.GetDirectoryName(path) ?? "";

            if (string.IsNullOrWhiteSpace(dir))
            {
                return false;
            }

            try
            {
                Directory.CreateDirectory(dir);
                await using var fileStream = File.Create(path);
                if (!stream.CanRead)
                {
                    return false;
                }

                await stream.CopyToAsync(fileStream);
            }
            catch (Exception e)
            {
                // fixme add logs
                return false;
            }

            return true;
        }
    }
}