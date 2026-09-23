using System.Text.Json.Serialization;

namespace SimpleChat.Model
{
    public class UserInfo
    {
        public Guid UserId { get; set; }

        public string LocalName { get; set; }

        [JsonConstructor]
        public UserInfo(Guid userId, string localName)
        {
            UserId = userId;
            LocalName = localName;
        }
    }
}
