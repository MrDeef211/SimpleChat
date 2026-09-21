using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleChat.MessageService
{
    // Для внутреннего использования IConnector, не использовать IConnector напрямую
    internal interface IMessageService
    {
        void SendMessage(string message);

        Task SendMessageAsync(string message);        
    }
}
