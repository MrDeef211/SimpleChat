using Microsoft.Extensions.DependencyInjection;
using SimpleChat.Extensions;
using SimpleChat.MessageService;

internal class Program
{
    private static void Main(string[] args)
    {
        var services = new ServiceCollection();

        services.AddChatServices();

        var serviceProvider = services.BuildServiceProvider();

    }
}