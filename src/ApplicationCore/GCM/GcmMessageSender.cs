using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Net;
using System.IO;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using ApplicationCore.Interfaces;
using Model;
using Model.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace ApplicationCore.GCM
{
    public class GcmMessageSender
    {
        public const string ERROR_MESSAGE_TOO_BIG = "MessageTooBig";
        private const bool DEBUG = false;
        private const string FCM_SEND_ENDPOINT = "https://fcm.googleapis.com/fcm/send";

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

        private readonly IRepository repository = null; // RepositoryFactory.GetRepository();

        public class Result
        {
            public string errorMessage = "";
            public bool severeErrorOccured = false;
            public bool invalidLegacyServerKey = false;

            public int success = 0;
            public int failure = 0;
            public int canonicalIds = 0;
            public int unregister = 0;

            public List<string> invalidRegistrations = new List<string>();
        }

        /// <summary>
        /// Doesn't throw exceptions
        /// </summary>
        /// <returns></returns>
        public Result Send(Dictionary<string, string> data, List<string> registrations, string fcmLegacyServerKey)
        {
            var jsonData = new JObject();

            foreach (KeyValuePair<string, string> kvp in data)
                jsonData.Add(kvp.Key, kvp.Value);

            return Send(jsonData, registrations, fcmLegacyServerKey);
        }

        private Result Send(JObject data, List<string> registrations, string fcmLegacyServerKey)
        {
            // prepare JSON body
            var json = new JObject();
            json.Add(JSON_PAYLOAD, data);

            var regIds = new JArray();
            foreach (var regId in registrations)
            {
                regIds.Add(regId);
            }

            json.Add(JSON_REGISTRATION_IDS, regIds);

            byte[] postData = Encoding.UTF8.GetBytes(json.ToString());


            // make POST request
            string apiKey = !string.IsNullOrEmpty(fcmLegacyServerKey)
                ? fcmLegacyServerKey
                : "";

            HttpWebRequest request = (HttpWebRequest) WebRequest.Create(FCM_SEND_ENDPOINT);
            request.Timeout = 120 * 1000;
            request.Method = "POST";
            request.ContentType = "application/json";
            request.ContentLength = postData.Length;
            request.Headers.Add("Authorization", "key=" + apiKey);

            // TODO: validate server certificate properly
            ServicePointManager.ServerCertificateValidationCallback += delegate(
                object sender,
                X509Certificate certificate,
                X509Chain chain,
                SslPolicyErrors sslPolicyErrors)
            {
                return true;
            };

            Stream requestStream = request.GetRequestStream();
            requestStream.Write(postData, 0, postData.Length);
            requestStream.Close();

            // get response
            Result result = new Result();
            result.severeErrorOccured = false;

            string responseStr = null;
            HttpStatusCode responseCode = HttpStatusCode.OK;
            int intResponseCode = 0;

            try
            {
                using (HttpWebResponse response = (HttpWebResponse) request.GetResponse())
                {
                    responseCode = response.StatusCode;
                    intResponseCode = (int) responseCode;
                    responseStr = new StreamReader(response.GetResponseStream()).ReadToEnd();
                    response.Close();
                }
            }
            catch (WebException webException)
            {
                if (webException.Status != WebExceptionStatus.ProtocolError
                    || webException.Response == null)
                {
                    result.errorMessage += "Could not get response from GCM endpoint: " + webException.Message;
                    result.severeErrorOccured = true;
                    return result;
                }

                using (HttpWebResponse response = (HttpWebResponse) webException.Response)
                {
                    responseCode = response.StatusCode;
                    intResponseCode = (int) responseCode;
                    responseStr = new StreamReader(response.GetResponseStream()).ReadToEnd();
                    response.Close();
                }
            }

            if (DEBUG)
                Debug.WriteLine(intResponseCode + ": " + responseStr);

            // analyze response code
            if (responseCode == HttpStatusCode.Unauthorized
                || responseCode == HttpStatusCode.Forbidden)
            {
                result.errorMessage += "API key is invalid";
                result.severeErrorOccured = true;

                if (!string.IsNullOrWhiteSpace(fcmLegacyServerKey))
                {
                    result.invalidLegacyServerKey = true;
                }

                return result;
            }

            if (responseCode == HttpStatusCode.BadRequest)
            {
                result.errorMessage += "Bad Request: \n"
                                       + intResponseCode + " \n\n" + responseStr + "\n\n "
                                       + " request body: " + json + "\n ";
                result.severeErrorOccured = true;
                return result;
            }

            // NOTE: You *MUST* use exponential backoff if you receive a 503 response code.

            // TODO retry sending instead of dropping these messages
            if (intResponseCode >= 500 && intResponseCode < 600)
            {
                result.errorMessage += "Internal Server Error response received from GCM endpoint: \n"
                                       + intResponseCode + " \n\n" + responseStr + "\n\n "
                                       + " while sending message to registration id: " + registrations[0] + "\n ";
                result.severeErrorOccured = false;
                return result;
            }

            // analize response body
            if (string.IsNullOrEmpty(responseStr))
            {
                result.errorMessage += "Got empty response from GCM endpoint. (code = " + intResponseCode + ")\n";
                result.severeErrorOccured = true;
                return result;
            }

            JObject responseObj;
            try
            {
                responseObj = JObject.Parse(responseStr);
            }
            catch (Exception ex)
            {
                // todo Logger.LogCmError("Error parsing JSON: \n" + ex.ToString());
                result.errorMessage += "Could not parse JSON response from GCM endpoint: \n" + intResponseCode + " \n\n"
                                       + responseStr + "\n";
                result.severeErrorOccured = true;
                return result;
            }

            result.success = responseObj[JSON_SUCCESS]?.ToObject<int>() ?? 0;
            result.failure = responseObj[JSON_FAILURE]?.ToObject<int>() ?? 0;
            result.canonicalIds = responseObj[JSON_CANONICAL_IDS]?.ToObject<int>() ?? 0;

            // TEST
            //result.failure = 1;

            // all messages sent successfully
            if (result.failure == 0 && result.canonicalIds == 0)
            {
                result.errorMessage = "";
                result.severeErrorOccured = false;
                return result; // success result
            }

            // process errors in response
            var jsonResults = responseObj[JSON_RESULTS]?.ToObject<JArray>();
            if (jsonResults != null)
            {
                StringBuilder errorStringBuilder = null;
                if (jsonResults.Count > 0)
                {
                    errorStringBuilder = new StringBuilder(result.errorMessage, jsonResults.Count * 275);
                }

                for (int i = 0; i < jsonResults.Count; i++)
                {
                    var jsonResult = jsonResults[i].ToObject<JObject>();
                    string messageId = JsonUtil.GetJsonValueString(jsonResult, JSON_MESSAGE_ID);

                    if (messageId != null)
                    {
                        var canonicalRegId = JsonUtil.GetJsonValueString(jsonResult, JSON_CANONICAL_REG_ID);
                        // test
                        //canonicalRegId = "test" + i;

                        if (canonicalRegId != null)
                        {
                            if (UpdateRegistrationId(registrations[i], canonicalRegId))
                                errorStringBuilder.Append("Success ");
                            else
                                errorStringBuilder.Append("Failure ");

                            errorStringBuilder.Append("updating current registration id: ");
                            errorStringBuilder.Append(registrations[i]);
                            errorStringBuilder.Append("\nto canonical registration id: ");
                            errorStringBuilder.Append(canonicalRegId);
                            errorStringBuilder.Append("\n\n");
                        }
                    }
                    else
                    {
                        string error = JsonUtil.GetJsonValueString(jsonResult, JSON_ERROR);

                        // TEST
                        //if (registrationIds[i].StartsWith("test")) error = ERROR_NOT_REGISTERED;

                        if (error != null)
                        {
                            if (error.Equals(ERROR_NOT_REGISTERED)
                                || error.Equals(ERROR_INVALID_REGISTRATION)
                                || error.Equals(ERROR_MISMATCH_SENDER_ID))
                            {
                                errorStringBuilder.Append("Error response from GCM endpoint: ");
                                errorStringBuilder.Append(error);
                                errorStringBuilder.Append(" => scheduled for deletion registrationId: ");
                                errorStringBuilder.Append(registrations[i]);
                                errorStringBuilder.Append("\n\n");

                                result.invalidRegistrations.Add(registrations[i]);
                                result.unregister++;
                            }
                            else if (error.Equals(ERROR_UNAVAILABLE) || error.Equals(ERROR_INTERNAL_SERVER_ERROR))
                            {
                                // TODO retry
                                errorStringBuilder.Append("Error response from GCM endpoint: ");
                                errorStringBuilder.Append(error);
                                errorStringBuilder.Append("\nfor registration id: ");
                                errorStringBuilder.Append(registrations[i]);
                                errorStringBuilder.Append("\n\n");
                            }
                            else if (error == ERROR_MESSAGE_TOO_BIG)
                            {
                                errorStringBuilder.Append(error);
                                result.severeErrorOccured = true;
                                break;
                            }
                            else
                            {
                                errorStringBuilder.Append("Error response from GCM endpoint: ");
                                errorStringBuilder.Append(error);
                                errorStringBuilder.Append("\nfor registration id: ");
                                errorStringBuilder.Append(registrations[i]);
                                errorStringBuilder.Append("\n\n");

                                result.severeErrorOccured = true;
                            }
                        }
                    }
                }

                if (errorStringBuilder != null)
                {
                    result.errorMessage = errorStringBuilder.ToString();
                }
            }
            else
            {
                result.errorMessage += "Unknown error. Please try again later\n\n";
                result.severeErrorOccured = true;
            }

            return result;
        }

        // TODO: batch these DB requests
        private bool UpdateRegistrationId(string oldRegistrationId, string newRegistrationId)
        {
            try
            {
                return repository.ExecuteNonQueryWithParams(
                    "UPDATE gcm_registrations SET registration_id=@2, modification_date = GETDATE() "
                    + "WHERE registration_id=@1",
                    new string[] {oldRegistrationId, newRegistrationId});
            }
            catch (Exception ex)
            {
                //Logger.LogCmError("Error updating registration\n" + ex.ToString());
                return false;
            }
        }
    }
}