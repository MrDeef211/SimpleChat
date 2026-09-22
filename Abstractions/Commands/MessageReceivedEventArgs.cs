using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Abstractions.Commands
{
    public class MessageReceivedEventArgs
    {
        public string Message { get; set; }

        public string Sender { get; set; }

        public DateTime SendTime { get; set; }

        public MessageReceivedEventArgs(string message, string sender, DateTime dateTime)
        {
            Message = message;
            Sender = sender;
            SendTime = dateTime;
        }
    }
}
