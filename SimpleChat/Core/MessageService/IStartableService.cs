namespace Abstractions.Core.MessageService
{
    public interface IStartableService
    {
        Task StartAsync(CancellationToken token);
        Task StopAsync(CancellationToken token);
    }
}
