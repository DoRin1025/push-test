using System.ComponentModel.DataAnnotations;

namespace Model
{
    public class AuthenticateModel
    {
        [Required] public string Username { get; set; }
        [Required] public string PublisherId { get; set; }
        [Required] public string ServerId { get; set; }
        [Required] public string ApiKey { get; set; }

        public bool IsValidModel()
            => !string.IsNullOrWhiteSpace(Username)
               && !string.IsNullOrWhiteSpace(PublisherId)
               && !string.IsNullOrWhiteSpace(ApiKey)
               && !string.IsNullOrWhiteSpace(ServerId);

        public string GetServerAndPubId() => ServerId + "_" + PublisherId;
    }
}