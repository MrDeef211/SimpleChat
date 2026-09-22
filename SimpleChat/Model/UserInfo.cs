using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

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
