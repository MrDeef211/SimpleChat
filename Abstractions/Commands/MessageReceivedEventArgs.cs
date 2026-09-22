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

        public string Sendler { get; set; }

        public DateTime SendTime { get; set; }
    }
}
