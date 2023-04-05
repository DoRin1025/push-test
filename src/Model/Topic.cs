using Microsoft.AspNetCore.Mvc;

namespace Model
{
    public class Topic
    {
         public string PublisherId { get; set; }
         public string AppOwnerUsername { get; set; }
        public string AppId { get; set; }
        public string DeviceId { get; set; }
         public string Type { get; set; }
     public string Topics { get; set; }


        public bool IsValid()
        {
            return PublisherId != null && !string.IsNullOrWhiteSpace(AppOwnerUsername) &&
                   !string.IsNullOrWhiteSpace(AppOwnerUsername) && !string.IsNullOrWhiteSpace(AppId) &&
                   !string.IsNullOrWhiteSpace(DeviceId) && !string.IsNullOrWhiteSpace(Type);
        }
    }
}