using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Abstractions.Core.MessageService
{
    public interface IStartableService
    {
        Task StartAsync(CancellationToken token);
        Task StopAsync(CancellationToken token);
    }
}
