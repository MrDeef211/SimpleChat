using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleChat.MessageService
{
    public interface IConnectionService
    {
        int Connect(Guid id, string user);

        Task<int> ConnectAsync(Guid id, string user);
    }
}
