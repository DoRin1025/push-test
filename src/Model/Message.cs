using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Model.PWA;
using Model.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Model
{
    public class Message
    {
        public const string PLATFORM_UNKNOWN = "unkown";
        public const string PLATFORM_GCM = "gcm";
        public const string PLATFORM_APN = "apn";
        public const string PLATFORM_WEB_PUSH = "web_push";

        public const string STATUS_UNKNOWN = "unknown";
        public const string STATUS_QUEUED = "queued";
        public const string STATUS_PROCESSING = "processing";
        public const string STATUS_DELIVERED = "delivered";
        public const string STATUS_FAILED = "failed";

        public const string ERROR_PAYLOAD_TOO_BIG = "payloadTooBig";
        public const string ERROR_OVER_CAPACITY = "overCapacity";
        public const string ERROR_APN_CONFIGURATION = "apnConfigurationError";
        public const string ERROR_INTERNAL_SERVER_ERROR = "internalServerError";
        public const string ERROR_FCM_CONFIGURATION = "fcmConfigurationError";

        public const string TYPE_DEFAULT = "announcement";

        public string Id { get; private set; }

        public string PublisherId { get; private set; }
        public string AppOwnerUsername { get; private set; }
        public string AppId { get; private set; }

        public string PwaPublicKey { set; get; }
        public string PwaPrivateKey { set; get; }
        private volatile List<PwaDeviceRegistration> pwaregistrations;

        public List<PwaDeviceRegistration> PwaRegistrations
        {
            get { return pwaregistrations; }
            set
            {
                pwaregistrations = value;
                RegistrationsTotal = value.Count;
            }
        }

        private volatile string status;

        public string Status
        {
            get { return status; }
            set
            {
                status = value;
                StatusChangedDate = DateTime.Now;
            }
        }

        public DateTime CreatedDate { get; private set; }
        public DateTime StatusChangedDate { get; private set; }

        public string Platform { get; private set; }
        public bool IsTestMessage { get; set; }
        public int QuickDeliveryTimeout { get; set; }
        public bool SendSynchronously { get; set; }
        public string FcmLegacyServerKey { get; set; }
        public bool SendLegacyGcm { get; set; }
        public string ServerEndPoint { get; set; }
        public string UniqueAppId { get; set; }
        private volatile int legacyGcmDelivered;

        public int LegacyGcmDelivered
        {
            get { return legacyGcmDelivered; }
            set { legacyGcmDelivered = value; }
        }

        private volatile List<string> deviceIds;

        public List<string> DeviceIds
        {
            get { return deviceIds; }
            set { deviceIds = value; }
        }

        private volatile List<string> registrations;
        private volatile List<string> fcmRegistrations;

        public List<string> Registrations
        {
            get { return registrations; }
            set
            {
                registrations = value;
                RegistrationsTotal = value.Count;
            }
        }

        public List<string> FcmRegistrations
        {
            get { return fcmRegistrations; }
            set
            {
                fcmRegistrations = value;
                RegistrationsTotal += value.Count;
            }
        }

        private volatile int registrationsTotal;

        public int RegistrationsTotal
        {
            get { return registrationsTotal; }
            set { registrationsTotal = value; }
        }

        private volatile int registrationsProcessed;

        public int RegistrationsProcessed
        {
            get { return registrationsProcessed; }
            set { registrationsProcessed = value; }
        }

        private volatile int registrationsDelivered;

        public int RegistrationsDelivered
        {
            get { return registrationsDelivered; }
            set { registrationsDelivered = value; }
        }

        private volatile int registrationsUpdated;

        public int RegistrationsUpdated
        {
            get { return registrationsUpdated; }
            set { registrationsUpdated = value; }
        }

        private volatile int registrationsFailed;

        public int RegistrationsFailed
        {
            get { return registrationsFailed; }
            set { registrationsFailed = value; }
        }

        private volatile int registrationsUnregistered;

        public int RegistrationsUnregistered
        {
            get { return registrationsUnregistered; }
            set { registrationsUnregistered = value; }
        }

        public string Topics { get; private set; }

        public Dictionary<string, string> Data { get; private set; }
        public string Type { get; private set; }
        public string Module { get; private set; }

        private volatile string errorReason;

        public string ErrorReason
        {
            get { return errorReason; }
            set { errorReason = value; }
        }

        private volatile string errorMessage;

        public string ErrorMessage
        {
            get { return errorMessage; }
            set { errorMessage = value; }
        }

        public JObject apnPayload { get; set; }

        [NonSerialized] private JObject ApnPayload;

        public JObject GetApnPayload()
        {
            return ApnPayload;
        }

        public void SetApnPayload(JObject apnPayload)
        {
            ApnPayload = apnPayload;
        }

        private Message()
        {
            this.Id = CryptoUtil.GetRandomAlphanumericString(32);
            this.CreatedDate = DateTime.Now;
            this.Status = STATUS_UNKNOWN;

            this.RegistrationsTotal = -1;

            this.ErrorMessage = "";
            this.ServerEndPoint = "";
        }

        public static Message FromJson(string jsonString)
        {
            JObject json = JObject.Parse(jsonString);

            Message message = new Message();

            message.PublisherId = JsonUtil.GetJsonValueString(json, "publisherId", SCEnvironment.ScPublisherId);
            message.AppOwnerUsername = JsonUtil.GetJsonValueString(json, "username");
            message.AppId = JsonUtil.GetJsonValueString(json, "appId");

            message.Platform = JsonUtil.GetJsonValueString(json, "platform", PLATFORM_UNKNOWN);

            if (json.ContainsKey("deviceIds"))
            {
                message.DeviceIds = ParseDeviceIds((JArray) json["deviceIds"]);
            }

            if (json.ContainsKey("fcmLegacyServerKey"))
                message.FcmLegacyServerKey = JsonUtil.GetJsonValueString(json, "fcmLegacyServerKey");

            if (json.ContainsKey("sendLegacyGcm"))
            {
                message.SendLegacyGcm = json.ContainsKey("sendLegacyGcm") &&
                                        (json["sendLegacyGcm"]?.ToObject<bool>() ?? false);
            }

            var messageData = json.ContainsKey("data") ? (JObject) json["data"] : null;

            if (messageData != null)
            {
                message.Type = JsonUtil.GetJsonValueString(messageData, "type", null);
                message.Module = JsonUtil.GetJsonValueString(messageData, "module", null);

                if (message.Type == null && message.Module == null)
                    throw new ArgumentException("Invalid JSON");

                message.Data = new Dictionary<string, string>();

                foreach (var kvp in messageData)
                {
                    message.Data.Add(kvp.Key, kvp.Value?.ToString());
                }
            }

            message.Topics = JsonUtil.GetJsonValueString(json, "topics", string.Empty);
            message.QuickDeliveryTimeout = json["quickDeliveryTimeout"]?.ToObject<int>() ?? 0;
            message.SendSynchronously = json["sendSynchronously"]?.ToObject<bool>() ?? false;
            message.UniqueAppId = json["uniqueAppId"]?.ToObject<string>() ?? "";

            if (string.IsNullOrEmpty(message.AppOwnerUsername) || string.IsNullOrEmpty(message.AppId) ||
                messageData == null)
            {
                throw new ArgumentException("Invalid JSON");
            }

            return message;
        }

        private static List<string> ParseDeviceIds(JArray jsonDeviceIds)
        {
            if (jsonDeviceIds == null)
            {
                return null;
            }

            var deviceIds = new List<string>( /* capacity: */ jsonDeviceIds.Count);
            deviceIds.AddRange(jsonDeviceIds.Select(jsonDeviceId => jsonDeviceId.ToString())
                .Where(deviceId => ValidationUtil.IsValidDeviceId(deviceId)));

            return deviceIds;
        }

        public string GetDataString()
        {
            StringBuilder builder = new StringBuilder();
            foreach (KeyValuePair<string, string> pair in this.Data)
            {
                builder.Append(pair.Key).Append("=").Append(pair.Value).Append(", ");
            }

            string result = builder.ToString();

            result = result.Substring(0, result.Length - 2);
            return result;
        }

        public string GetDataShortString()
        {
            StringBuilder builder = new StringBuilder();
            foreach (KeyValuePair<string, string> pair in this.Data)
            {
                string value = pair.Value;
                if (pair.Key == "message" || pair.Key == "alert" || pair.Key == "text")
                {
                    value = value.Length > 20 ? value.Substring(0, 20) + "...(truncated)" : value;
                    value = value.Replace("\n", "\\n");
                }

                builder.Append(pair.Key).Append("=").Append(value).Append(", ");
            }

            string result = builder.ToString();

            result = result.Substring(0, result.Length - 2);
            return result;
        }

        // Returns "'12345678','abscdef12'", or "null" if there are no valid items
        public string GetDeviceIdsAsSqlListString()
        {
            if (deviceIds == null)
            {
                return "null";
            }

            StringBuilder builder = new StringBuilder();
            foreach (string deviceId in deviceIds)
            {
                // Validating device id: only alphanumerics, and '-'; length from 16 to 36
                if (!ValidationUtil.IsValidDeviceId(deviceId))
                    continue;

                if (builder.Length > 0)
                {
                    builder.Append(",");
                }

                builder.Append("'").Append(deviceId).Append("'");
            }

            return (builder.Length > 0 ? builder.ToString() : "null");
        }

        public override string ToString()
        {
            string errorMessageShort =
                ErrorMessage.Length > 100 ? ErrorMessage.Substring(0, 100) + "..." : ErrorMessage;

            return "[Message: id=" + Id
                                   + ", status=" + Status
                                   + ", created=" + CreatedDate
                                   + ", statusChanged=" + StatusChangedDate
                                   + ", platform=" + Platform
                                   + ", appId=" + PublisherId + "." + AppOwnerUsername + "." + AppId
                                   + ", deviceIds=[" + GetDeviceIdsAsSqlListString() + "]"
                                   + ", registrations={total=" + RegistrationsTotal
                                   + ", processed=" + RegistrationsProcessed
                                   + ", delivered=" + RegistrationsDelivered
                                   + ", updated=" + RegistrationsUpdated
                                   + ", failed=" + RegistrationsFailed
                                   + ", unregistered=" + RegistrationsUnregistered
                                   + "}"
                                   + ", isBig=" + IsBig()
                                   + ", dataShort={" + GetDataShortString() + "}"
                                   + ", errorReason=" + ErrorReason
                                   + ", errorMessage (" + ErrorMessage.Length + ")=" + errorMessageShort
                                   + ", serverEndPoint=" + ServerEndPoint
                                   + ", legacyGcmDelivered=" + LegacyGcmDelivered
                                   + "]";
        }

        public bool IsBig()
        {
            // this should be delivered in about 50 seconds
            return RegistrationsTotal > 50000;
        }

        public string ToJsonString()
        {
            var json = new JObject
            {
                {"id", Id},
                {"publisherId", PublisherId},
                {"appOwnerUsername", AppOwnerUsername},
                {"appId", AppId},
                {"status", Status},
                {"createdDate", ApiUtil.DateToString(CreatedDate)},
                {"statusChangedDate", ApiUtil.DateToString(StatusChangedDate)},
                {"platform", Platform},
                {"isTestMessage", IsTestMessage}
            };


            var registrations = new JObject();
            json.Add("registrations", registrations);
            registrations.Add("total", RegistrationsTotal);
            registrations.Add("processed", RegistrationsProcessed);
            registrations.Add("delivered", RegistrationsDelivered);
            registrations.Add("updated", RegistrationsUpdated);
            registrations.Add("failed", RegistrationsFailed);
            registrations.Add("unregistered", RegistrationsUnregistered);

            json.Add("isBig", IsBig());

            json.Add("topics", Topics);

            var data = new JObject();
            json.Add("data", data);

            foreach (KeyValuePair<string, string> kvp in Data)
                data.Add(kvp.Key, kvp.Value);

            if (!string.IsNullOrEmpty(ErrorReason))
            {
                var error = new JObject();
                json.Add("deliveryError", error);
                error.Add("reason", ErrorReason);
                error.Add("message", ErrorMessage);
            }

            if (LegacyGcmDelivered > 0)
            {
                json.Add("legacyGcmDelivered", LegacyGcmDelivered);
            }

            if (apnPayload != null)
                json.Add("apn_payload", apnPayload);
            return json.ToString();
        }

        public class Builder
        {
            private Message message;

            public Builder(string platform)
            {
                message = new Message();
                message.Platform = platform;

                message.PublisherId = SCEnvironment.ScPublisherId;
                message.AppOwnerUsername = "";
                message.AppId = "";

                message.Data = new Dictionary<string, string>();

                message.Topics = "";
            }

            public Builder PublisherId(string publisherId)
            {
                message.PublisherId = publisherId;
                return this;
            }

            public Builder AppOwnerUsername(string username)
            {
                message.AppOwnerUsername = username;
                return this;
            }

            public Builder AppId(string appId)
            {
                message.AppId = appId;
                return this;
            }

            /// <summary>
            /// This is equivalient of Data("module", module)
            /// </summary>
            public Builder Module(string module)
            {
                message.Data["module"] = module;
                return this;
            }

            /// <summary>
            /// This is equivalient of Data("type", type)
            /// </summary>
            public Builder Type(string type)
            {
                message.Data["type"] = type;
                return this;
            }

            /// <summary>
            /// This is equivalient of Data("alert", alert)
            /// </summary>
            public Builder Alert(string alert)
            {
                message.Data["alert"] = alert;
                return this;
            }

            /// <summary>
            /// This is equivalient of Data("badge", badge)
            /// </summary>
            public Builder Badge(string badge)
            {
                message.Data["badge"] = badge;
                return this;
            }

            /// <summary>
            /// This is equivalient of Data("badge", badge.ToString())
            /// </summary>
            public Builder Badge(int badge)
            {
                message.Data["badge"] = badge.ToString();
                return this;
            }

            /// <summary>
            /// This is equivalient of Data("sound", sound)
            /// </summary>
            public Builder Sound(string sound)
            {
                message.Data["sound"] = sound;
                return this;
            }

            public Builder Data(string key, string value)
            {
                message.Data[key] = value;
                return this;
            }

            public Builder Topics(string topics)
            {
                message.Topics = topics;
                return this;
            }

            public Builder IsTestMessage(bool isTest)
            {
                message.IsTestMessage = isTest;
                return this;
            }

            public Builder RegistrationsTotal(int total)
            {
                message.RegistrationsTotal = total;
                return this;
            }

            public Message build()
            {
                message.Module = message.Data.ContainsKey("module") ? message.Data["module"] : null;
                message.Type = message.Data.ContainsKey("type") ? message.Data["type"] : null;

                return message;
            }
        }
    }
}