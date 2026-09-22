using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Abstractions.DTO
{
    public class PingDTO
    {
        public Guid Sendler { get; set; }

        public DateTime PingTime { get; set; }

        public string? reason { get; set; }
    }
}
