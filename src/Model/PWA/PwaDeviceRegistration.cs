namespace Model.PWA
{
    public class PwaDeviceRegistration : DeviceRegistration
    {
        public PwaDeviceRegistration()
        {
        }

        public PwaDeviceRegistration(DeviceRegistration device)
        {
            AppId = device.AppId;
            PublisherId = device.PublisherId;
            AppOwnerUsername = device.AppOwnerUsername;
            DeviceId = device.DeviceId;
            Platform = device.Platform;
        }

        public string Endpoint { set; get; }
        public string ExpirationTime { set; get; }
        public string P256dh { set; get; }
        public string Auth { set; get; }

        public string ToString()
        {
            return "DeviceRegistration: "
                   + "PublisherId=" + PublisherId
                   + ", UserName=" + AppOwnerUsername
                   + ", AppId=" + AppId
                   + ", DeviceId=" + DeviceId
                   + ", Platform=" + Platform
                   + ", Platform=" + Endpoint
                   + ", ExpirationTime=" + ExpirationTime
                   + ", P256dh=" + P256dh
                   + ", Auth=" + Auth;
        }
    }
}