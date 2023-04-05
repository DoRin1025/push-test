using System;

namespace Model.APNS
{
    public class ApnResponse
    {
        public bool IsSuccess { get; set; }
        public string DeviceToken { get; set; }
        public ApnError Error { get; set; }
        public Exception Exception { get; set; }
    }

    public class ApnError
    {
        public ReasonEnum Reason { get; set; }
        public long? Timestamp { get; set; }

        /// <summary>
        /// https://developer.apple.com/library/archive/documentation/NetworkingInternet/Conceptual/RemoteNotificationsPG/CommunicatingwithAPNs.html#//apple_ref/doc/uid/TP40008194-CH11-SW15
        /// </summary>
        public enum ReasonEnum
        {
            BadCollapseId,
            BadDeviceToken,
            BadExpirationDate,
            BadMessageId,
            BadPriority,
            BadTopic,
            DeviceTokenNotForTopic,
            DuplicateHeaders,
            IdleTimeout,
            InvalidPushType,
            MissingDeviceToken,
            MissingTopic,
            PayloadEmpty,
            TopicDisallowed,
            BadCertificate,
            BadCertificateEnvironment,
            ExpiredProviderToken,
            Forbidden,
            InvalidProviderToken,
            MissingProviderToken,
            BadPath,
            MethodNotAllowed,
            Unregistered,
            PayloadTooLarge,
            TooManyProviderTokenUpdates,
            TooManyRequests,
            InternalServerError,
            ServiceUnavailable,
            Shutdown,
        }
    }
}