using System.Net;
using Newtonsoft.Json.Linq;

namespace Model
{
    public class ApiError : JObject
    {
        public ApiError() { }

        /// <summary>
        /// If you use this contructor you must add one or more errors using AddError()
        /// </summary>
        public ApiError(HttpStatusCode code, string message)
        {
            JArray errors = new JArray();
            this.Add(new JProperty("errors", errors));

            this.Add("code", (int)code);
            this.Add("message", message);
        }

        public ApiError(HttpStatusCode code, string reason, string message)
            : this(code, message)
        {
            AddError(reason, message);
        }

        public ApiError AddError(string reason, string message)
        {
            JArray errors = (JArray)this["errors"];

            JObject error = new JObject
            {
                { "reason", reason },
                { "message", message }
            };

            errors.Add(error);

            return this;
        }

        public void SetError( HttpStatusCode code, string reason, string message)
        {
            SetError( new ApiError(code, reason, message));
        }

        public void SetError(ApiError error)
        {
            this["error"] = error;
        }
    }
}