using System;
using System.Globalization;
using System.Linq;
using System.Net;
using Newtonsoft.Json.Linq;

namespace Model.Utils
{
    public class ApiUtil
    {
        /// <summary>
        /// Converts the date to RFC 3339 format with UTC timezone
        /// </summary>
        /// <param name="date"></param>
        /// <returns></returns>
        public static string DateToString(DateTime date)
        {
            return date.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffK");
        }

        /// <summary>
        /// Parses the date from RFC 3339 format with UTC timezone
        /// </summary>
        /// <param name="dateString"></param>
        /// <returns></returns>
        public static DateTime DateFromString(string dateString)
        {
            return DateTime.ParseExact(dateString, "yyyy-MM-ddTHH:mm:ss.fffK", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Parses the date from RFC 3339 format with UTC timezone. Will return DateTime.MinValue if parsing fails.
        /// </summary>
        /// <param name="dateString"></param>
        /// <returns></returns>
        public static DateTime DateFromStringSafe(string dateString)
        {
            try
            {
                return DateTime.ParseExact(dateString, "yyyy-MM-ddTHH:mm:ss.fffK", CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                return DateTime.MinValue;
            }
        }

        public static void AddObjectOrNull(JObject response, string key, JArray obj)
        {
            response.Add(key, obj);
        }

        public static void SetError(JObject response, HttpStatusCode code, string reason, string message)
        {
            SetError(response, new ApiError(code, reason, message));
        }

        public static void SetError(JToken response, ApiError error)
        {
            response["error"] = error;
        }

        public static void AddMethodError(JToken response, string[] supportedMethods)
        {
            var methodsString = string.Join(", ", supportedMethods);

            var error = new ApiError(HttpStatusCode.MethodNotAllowed,
                "methodNotAllowed",
                "Only these methods are allowed for specified resource: " + methodsString);

            JObject headers = new JObject();
            headers.Add("Allow", methodsString);

            error.Add("headers", headers);

            SetError(response, error);
        }

        public static bool ValidateMethod(JObject response, string requestMethod, string supportedMethod)
        {
            if (requestMethod != supportedMethod)
            {
                AddMethodError(response, new string[] {supportedMethod});
                return false;
            }

            return true;
        }

        public static bool ValidateMethod(JObject response, string requestMethod, string[] supportedMethods)
        {
            var supported = supportedMethods.Any(method => requestMethod == method);

            if (!supported)
            {
                AddMethodError(response, supportedMethods);
            }

            return supported;
        }

        public static bool ValidateContentType(JObject response, string contentType, string supportedContentType)
        {
            int typeEndIndex = contentType.IndexOf(";");
            if (typeEndIndex != -1)
                contentType = contentType.Substring(0, typeEndIndex);

            if (contentType == supportedContentType)
            {
                return true;
            }
            else
            {
                SetError(response, HttpStatusCode.BadRequest, "unsupportedContentType",
                    "Content type for this request must be " + supportedContentType);

                return false;
            }
        }

        public static string GetIpAddress(string ipAddress)
        {
            if (ipAddress == null)
                ipAddress = "";

            return ipAddress;
        }

        /// <summary>
        /// For debug only!!!!
        /// </summary>
        public static void AddDebugException(JObject response, Exception e)
        {
            JArray exceptions = null;

            if (response.ContainsKey("exceptions"))
            {
                exceptions = response["exceptions"] as JArray;
            }
            else
            {
                exceptions = new JArray();
                response.Add("exceptions", exceptions);
            }

            JObject ex = new JObject();
            ex.Add("name", e.GetType().ToString());
            ex.Add("message", e.Message);
            ex.Add("toString", e.ToString());

            exceptions.Add(ex);
        }
    }
}