using Microsoft.AspNetCore.Mvc;

namespace Model
{
    public class DeviceRegistration
    {
        public string PublisherId { get; set; }
        [BindProperty(Name = "Username")] public string AppOwnerUsername { get; set; }
        public string AppId { get; set; }

        public string DeviceId { get; set; }
        public string RegistrationId { get; set; }

        public string Platform { get; set; }
        public int ScmVersion { get; set; }


        public DeviceRegistration()
        {
            ScmVersion = 1;
        }

        public string ToString()
        {
            return "DeviceRegistration: "
                   + "PublisherId=" + PublisherId
                   + ", AppOwnerUsername=" + AppOwnerUsername
                   + ", AppId=" + AppId
                   + ", DeviceId=" + DeviceId
                   + ", RegistrationId=" + RegistrationId
                   + ", Platform=" + Platform
                   + ", ScmVersion=" + ScmVersion;
        }

        public bool IsValid(out string validationArgs)
        {
            validationArgs = string.Join(',',
                new string[]
                {
                    nameof(AppOwnerUsername),
                    nameof(AppId),
                    nameof(DeviceId),
                    nameof(RegistrationId),
                    nameof(Platform)
                });

            return !string.IsNullOrWhiteSpace(AppOwnerUsername)
                   && !string.IsNullOrWhiteSpace(AppId)
                   && !string.IsNullOrWhiteSpace(DeviceId)
                   && !string.IsNullOrWhiteSpace(RegistrationId)
                   && !string.IsNullOrWhiteSpace(Platform);
        }
    }
}