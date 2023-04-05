using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Model.APNS;
using Model.Utils;

namespace ApplicationCore.APNS
{
    /// Source of Copyright: https://github.com/andrei-m-code/net-core-push-notifications
    /// MIT License https://github.com/andrei-m-code/net-core-push-notifications/blob/master/LICENSE.md
    /// <summary>
    /// Apple Push Notification sender
    /// </summary>
    public class ApnMessageSender : IDisposable
    {
        public const string ErrorApnCertificateMissing = "APNs Certificate is missing";
        public const string ErrorApnCertificateExpired = "APNs Certificate has expired";

        private enum ApnServerType
        {
            Development,
            Production
        }

        public enum ApplePushType
        {
            Unknown,
            Alert,
            Background,
            Voip
        }

        private static readonly Dictionary<ApnServerType, string> Servers = new Dictionary<ApnServerType, string>
        {
            {ApnServerType.Development, "https://api.development.push.apple.com"},
            {ApnServerType.Production, "https://api.push.apple.com"}
        };

        private readonly string _appBundleId;
        private readonly ApnServerType _server;
        private readonly Lazy<HttpClient> _httpClient;
        private ApnPayload _apnPayload;

        /// <summary>
        /// Initialize sender using Provider Certificate.
        /// </summary>
        /// <param name="cert">p12 certificate generated in your developer account.</param>
        /// <param name="apnPayload"></param>
        /// <param name="appBundleId">App’s bundle ID e.g. "com.AppName". Obtain this value from your developer account.</param>
        /// <param name="useDevServer">Development or Production server.</param>
        public ApnMessageSender(X509Certificate2 cert, bool useDevServer, ApnPayload apnPayload, string appBundleId)
        {
            ApnServerType apnServer = useDevServer ? ApnServerType.Development : ApnServerType.Production;
            ValidateApnsCertificate(cert, apnServer, appBundleId);
            _server = apnServer;
            _apnPayload = apnPayload;
            _appBundleId = appBundleId;

            var httpClientHandler = new HttpClientHandler {ClientCertificateOptions = ClientCertificateOption.Manual};
            httpClientHandler.ClientCertificates.Add(cert);
            _httpClient = new Lazy<HttpClient>(() => new HttpClient(httpClientHandler));
        }

        /// <summary>
        /// Serialize and send notification to APN.
        /// !IMPORTANT: If you send many messages at once, make sure to retry those calls. Apple typically doesn't like 
        /// to receive too many requests and may ocasionally respond with HTTP 429. Just try/catch this call and retry as needed.
        /// <para>
        /// APNs request headers:
        /// <see cref="http://developer.apple.com/library/archive/documentation/NetworkingInternet/Conceptual/RemoteNotificationsPG/CommunicatingwithAPNs.html"/>
        /// </para>
        /// </summary>
        /// <param name="pushType">Type of push notification.</param>
        /// <param name="deviceToken">Device identifier token.</param>
        /// <param name="apnsExpiration">This header identifies the date when the notification is no longer valid and can be discarded.</param>
        /// <param name="apnsPriority">The priority of the notification. Possible values: 10, 5.</param>
        /// <exception cref="HttpRequestException">Throws exception when not successful.</exception>
        public async Task<ApnResponse> SendAsync(
            string deviceToken,
            int apnsExpiration = 0,
            int apnsPriority = 10,
            ApplePushType pushType = ApplePushType.Alert)
        {
            try
            {
                var path = $"/3/device/{deviceToken}";

                string json;
                if (_apnPayload != null)
                {
                    json = _apnPayload.GeneratePayload();
                }
                else
                {
                    throw new ArgumentNullException($"{nameof(_apnPayload)}");
                }

                var request = new HttpRequestMessage(HttpMethod.Post, new Uri(Servers[this._server] + path))
                {
                    Version = new Version(2, 0),
                    Content = new StringContent(json)
                };

                request.Headers.TryAddWithoutValidation(":method", "POST");
                request.Headers.TryAddWithoutValidation(":path", path);

                request.Headers.Add("apns-topic", this._appBundleId);
                request.Headers.Add("apns-expiration", apnsExpiration.ToString());
                request.Headers.Add("apns-priority", apnsPriority.ToString());
                request.Headers.Add("apns-push-type", pushType.ToString().ToLower());

                var response = await _httpClient.Value.SendAsync(request);

                bool succeed = response.IsSuccessStatusCode;
                string content = await response.Content.ReadAsStringAsync();
                ApnError error = JsonUtil.Deserialize<ApnError>(content);

                return new ApnResponse
                {
                    IsSuccess = succeed,
                    DeviceToken = deviceToken,
                    Error = error
                };
            }
            catch (Exception ex)
            {
                return new ApnResponse
                {
                    IsSuccess = false,
                    DeviceToken = deviceToken,
                    Exception = ex
                };
            }
        }

        public void Dispose()
        {
            if (_httpClient.IsValueCreated)
            {
                _httpClient.Value.Dispose();
            }
        }

        /// <summary>
        /// Validation APN Service SSL Certificate.
        /// </summary>
        /// <remarks>
        /// You should ensure that this validation is always updated with the new format of the APN Service SSL Certificate.
        /// </remarks>
        private static void ValidateApnsCertificate(X509Certificate2 cert, ApnServerType apnServerType,
            string appBundleId)
        {
            if (cert != null)
            {
                var issuerName = cert.IssuerName.Name;
                var subjectName = cert.SubjectName.Name;

                if (issuerName == null || subjectName == null)
                {
                    throw new ArgumentException(
                        "Your certificate does not appear to be issued by Apple! Please check to ensure you have the correct certificate!");
                }

                if (!issuerName.Contains("Apple"))
                {
                    throw new ArgumentException(
                        "Your certificate does not appear to be issued by Apple! Please check to ensure you have the correct certificate!");
                }

                if (!Regex.IsMatch(subjectName, "Apple.*?Push Services")
                    && !subjectName.Contains("Website Push ID:"))
                {
                    throw new ArgumentException(
                        "Your certificate is not a valid certificate for connecting to Apple's APNS servers.");
                }

                if (subjectName.Contains("Development") && apnServerType != ApnServerType.Development)
                {
                    throw new ArgumentException(
                        "You are using a certificate created for connecting only to the Development APNS server but have selected a different server environment to connect to.");
                }

                if (subjectName.Contains("Production") && apnServerType != ApnServerType.Production)
                {
                    throw new ArgumentException(
                        "You are using a certificate created for connecting only to the Production APNS server but have selected a different server environment to connect to.");
                }

                if (!subjectName.Contains(appBundleId))
                {
                    throw new ArgumentException(
                        "You specified the wrong appBundleId, it does not match the one specified in the certificate.");
                }
            }
            else
            {
                throw new ArgumentException("You must provide a certificate to connect to APNS with!");
            }
        }
    }
}