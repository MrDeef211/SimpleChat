using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleChat.Model
{
    internal class UserInfo
    {
        public Guid UserId { get; set; }

        public string LocalName { get; set; }

        public UserInfo(Guid id, string name) 
        {
            UserId = id;
            LocalName = name;
        }

    }
}
