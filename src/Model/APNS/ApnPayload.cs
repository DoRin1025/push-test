using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Text;
using Model.Utils;

namespace Model.APNS
{
    /// Source of Copyright: https://github.com/alexalok/dotAPNS
    /// Apache License Version 2.0, January 2004 https://github.com/alexalok/dotAPNS/blob/master/LICENSE
    /// Changed by: Ghincul Vladimir
    /// <summary>
    /// Class to help create APN JSON Payload
    /// JSON Payload documentation:
    /// <see cref="https://developer.apple.com/documentation/usernotifications/setting_up_a_remote_notification_server
    /// /generating_a_remote_notification"/>
    /// </summary>
    public class ApnPayload
    {
        public class ApplePushAlert
        {
            public string Title { get; private set; }

            public string Body { get; private set; }

            public ApplePushAlert(string title, string body)
            {
                Title = title;
                Body = body;
                if (body == null)
                {
                    throw new ArgumentNullException(nameof(body));
                }
            }
        }

        /// <summary>
        /// For all not VoIP notifications, the maximum payload size is 4 KB (4096 bytes).
        /// </summary>
        public const int MAX_PAYLOAD_SIZE = 4096;

        /// <summary>
        /// If specified, this value will be used as a 'apns-
        /// </summary>
        public int? CustomPriority { get; private set; }

        /// <summary>
        /// This value could be null sometimes, so checking for null is required before its usage.
        /// </summary>
        public ApplePushAlert Alert { get; private set; }

        public int? Badge { get; private set; }

        /// <summary>
        /// This value could be null sometimes, so checking for null is required before its usage.
        /// </summary>
        public string Sound { get; private set; }

        /// <summary>
        /// This value could be null sometimes, so checking for null is required before its usage.
        /// Official documentation for reference:
        /// <see cref="https://developer.apple.com/documentation/usernotifications/unnotificationcontent/1649866-categoryidentifier"/>
        /// </summary>
        public string Category { get; private set; }

        public bool IsContentAvailable { get; private set; }

        public bool IsMutableContent { get; private set; }

        /// <summary>
        /// User-defined properties that will be attached to the root payload dictionary.
        /// </summary>
        public Dictionary<string, object> CustomProperties { get; set; }

        /// <summary>
        /// User-defined properties that will be attached to the <i>aps</i> payload dictionary.
        /// </summary>
        public IDictionary<string, object> CustomApsProperties { get; set; }

        /// <summary>
        /// Indicates whether alert must be sent as a string. 
        /// </summary>
        private bool sendAlertAsText;

        /// <summary>
        /// Add `content-available: 1` to the payload.
        /// </summary>
        public ApnPayload AddContentAvailable()
        {
            IsContentAvailable = true;
            return this;
        }

        /// <summary>
        /// Add `mutable-content: 1` to the payload.
        /// </summary>
        /// <returns></returns>
        public ApnPayload AddMutableContent()
        {
            IsMutableContent = true;
            return this;
        }

        /// <summary>
        /// Add alert to the payload.
        /// </summary>
        /// <param name="title">Alert title. Can be null.</param>
        /// <param name="body">Alert body. <b>Cannot be null.</b></param>
        /// <returns></returns>
        public ApnPayload AddAlert(string title = null, string body = null)
        {
            Alert = new ApplePushAlert(title, body);
            if (title == null)
            {
                sendAlertAsText = true;
            }

            return this;
        }

        public ApnPayload SetPriority(int priority)
        {
            if (priority < 0 || priority > 10)
            {
                throw new ArgumentOutOfRangeException(nameof(priority), priority, "Priority must be between 0 and 10.");
            }

            CustomPriority = priority;
            return this;
        }

        public ApnPayload AddBadge(int badge)
        {
            IsContentAvailableGuard();
            if (Badge != null)
            {
                throw new InvalidOperationException(nameof(Badge) + " already exists");
            }

            Badge = badge;
            return this;
        }

        public ApnPayload AddSound(string sound = "default")
        {
            if (string.IsNullOrWhiteSpace(sound))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(sound));
            }

            IsContentAvailableGuard();
            if (Sound != null)
            {
                throw new InvalidOperationException(nameof(Sound) + " already exists");
            }

            Sound = sound;
            return this;
        }

        public ApnPayload AddCategory(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(category));
            }

            if (Category != null)
            {
                throw new InvalidOperationException(string.Format("{0} already exists.", nameof(Category)));
            }

            Category = category;
            return this;
        }

        /// <summary>
        /// Add custom propery to APN JSON Payload
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="addToApsDict">If <b>true</b>, property will be added to the <i>aps</i> dictionary, otherwise to the root dictionary. Default: <b>false</b>.</param>
        /// <returns></returns>
        public ApnPayload AddCustomProperty(string key, object value, bool addToApsDict = false)
        {
            if (addToApsDict)
            {
                if (CustomApsProperties == null)
                {
                    CustomApsProperties = new Dictionary<string, object>();
                }

                CustomApsProperties.Add(key, value);
            }
            else
            {
                if (CustomProperties == null)
                {
                    CustomProperties = new Dictionary<string, object>();
                }

                CustomProperties.Add(key, value);
            }

            return this;
        }

        public string GeneratePayload()
        {
            dynamic payload = new ExpandoObject();
            payload.aps = new ExpandoObject();
            IDictionary<string, object> apsAsDict = payload.aps;

            if (IsContentAvailable)
            {
                apsAsDict["content-available"] = "1";
            }

            if (IsMutableContent)
            {
                apsAsDict["mutable-content"] = "1";
            }

            if (Alert != null)
            {
                object alert;
                if (sendAlertAsText)
                {
                    alert = Alert.Body;
                }
                else
                {
                    alert = new {title = Alert.Title, body = Alert.Body};
                }

                payload.aps.alert = alert;
            }

            if (Badge != null)
            {
                payload.aps.badge = Badge.Value;
            }

            if (Sound != null)
            {
                payload.aps.sound = Sound;
            }

            if (Category != null)
            {
                payload.aps.category = Category;
            }

            if (CustomProperties != null)
            {
                IDictionary<string, object> payloadAsDict = payload;
                foreach (var customProperty in CustomProperties)
                {
                    payloadAsDict[customProperty.Key] = customProperty.Value;
                }
            }

            if (CustomApsProperties != null)
            {
                foreach (var customApsProperty in CustomApsProperties)
                {
                    apsAsDict[customApsProperty.Key] = customApsProperty.Value;
                }
            }

            payload = JsonUtil.Serialize(payload);
            if (Encoding.UTF8.GetBytes(payload).Length > MAX_PAYLOAD_SIZE)
            {
                throw new ArgumentOutOfRangeException(nameof(payload), payload,
                    "Payload too large (must be " + MAX_PAYLOAD_SIZE + " bytes or smaller");
            }

            return payload;
        }

        private void IsContentAvailableGuard()
        {
            if (IsContentAvailable)
            {
                throw new InvalidOperationException("Cannot add fields to a push with content-available");
            }
        }
    }
}