using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleChat.MessageHandler
{
    internal interface IMessageHandler
    {
        event EventHandler<EventArgs> MessageReceived;
    }
}
