using System;
using System.Collections.Generic;
using System.Net;
using Model;
using Model.PWA;
using WebPush;

using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

/// <summary>
/// <para>@author Alexandru Capustean</para>
/// </summary>
public class PwaMessageSender
{
    public const string ERROR_MESSAGE_TOO_BIG = "MessageTooBig";

    private const bool DEBUG = false;

    private const string ERROR_QUOTA_EXCEEDED = "QuotaExceeded";
    private const string ERROR_DEVICE_QUOTA_EXCEEDED = "DeviceQuotaExceeded";
    private const string ERROR_INVALID_REGISTRATION = "InvalidRegistration";
    private const string ERROR_NOT_REGISTERED = "NotRegistered";
    private const string ERROR_MISSING_COLLAPSE_KEY = "MissingCollapseKey";
    private const string ERROR_UNAVAILABLE = "Unavailable";
    private const string ERROR_INTERNAL_SERVER_ERROR = "InternalServerError";
    private const string ERROR_MISMATCH_SENDER_ID = "MismatchSenderId";

    private const string JSON_REGISTRATION_IDS = "registration_ids";
    private const string JSON_PAYLOAD = "data";
    private const string JSON_SUCCESS = "success";
    private const string JSON_FAILURE = "failure";
    private const string JSON_CANONICAL_IDS = "canonical_ids";
    private const string JSON_RESULTS = "results";
    private const string JSON_MESSAGE_ID = "message_id";
    private const string JSON_ERROR = "error";
    private const string JSON_CANONICAL_REG_ID = "registration_id";

    public class Result
    {
        public string errorMessage = "";
        public bool severeErrorOccured = false;

        public int success = 0;
        public int failure = 0;
        public int canonicalIds = 0;
        public int unregister = 0;

        public List<string> invalidRegistrations = new List<string>();
    }

    public static Result Send(Message message, List<PwaDeviceRegistration> registrations)
    {
        Dictionary<string, string> data = message.Data;

        JObject notifData = new JObject();
        foreach (KeyValuePair<string, string> kvp in data)
        {
            if (kvp.Key == "button_text")
                notifData.Add("title", kvp.Value);
            else if (kvp.Key == "message")
                notifData.Add("body", kvp.Value);
            else if ((kvp.Key == "local_img"|| kvp.Key == "external_img") && !string.IsNullOrEmpty(kvp.Value))
            {
                notifData.Add("icon", kvp.Value);
                notifData.Add("image", kvp.Value);
            }
            else if ((kvp.Key == "action" || kvp.Key == "external_action") && !string.IsNullOrEmpty(kvp.Value))
                notifData.Add("custom_action", kvp.Value);

        }

        Result result = new Result();
        result.severeErrorOccured = false;


        foreach (var registration in registrations)
        {
            try
            {
                // todo add to infrastructure
                PushSubscription subscription = null;
                VapidDetails vapidDetails = null;

                var pushEndpoint = registration.Endpoint;
                string p256dh = registration.P256dh;
                string auth = registration.Auth;


                var subject = @"mailto:sc.support@mobilesoft.com";
                /*
                    "mailto:" string as well. This string needs to be either a URL or a mailto email address. This piece of information will actually be sent to web push service as part of the request to trigger a push. The reason this is done is so that if a web push service needs to get in touch with the sender, they have some information that will enable them to.
                    source: https://developers.google.com/web/fundamentals/push-notifications/sending-messages-with-web-push-libraries
                 */


                var publicKey = message.PwaPublicKey;
                var privateKey = message.PwaPrivateKey;

                subscription = new PushSubscription(pushEndpoint, p256dh, auth);
                vapidDetails = new VapidDetails(subject, publicKey, privateKey);

                var webPushClient = new WebPushClient();

                webPushClient.SendNotification(subscription, JsonConvert.SerializeObject(notifData), vapidDetails);
                result.success += 1;
            }
            catch (WebPushException ex)
            {
                if (ex.StatusCode == HttpStatusCode.Gone)
                {
                    result.invalidRegistrations.Add(registration.DeviceId);
                    result.unregister += 1;
                }
            }
            catch (Exception ex)
            {

                if (ex.Message.Equals("One or more errors occurred."))
                {
                    result.invalidRegistrations.Add(registration.DeviceId);
                    result.failure += 1;
                }
                else
                {
                    result.failure += 1;
                //    Logger.LogPWAError("webPushClient SendNotification() throw new exception" + ex.ToString());
                }
            }
        }

        return result;
    }
}