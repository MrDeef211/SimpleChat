using System.Windows;
using GUI.Configuration;
using GUI.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using SimpleChat.Extensions;

namespace GUI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private IServiceCollection _services;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var settings = StartupSettings.Load();
            if (settings.LaunchConsole)
            {
                ConsoleHelper.Attach();
                if (settings.HideConsoleOnStart)
                    ConsoleHelper.Hide();

                Console.WriteLine($"[{System.DateTime.Now:HH:mm:ss}] Приложение запущено.");
            }

            _services = new ServiceCollection();

            _services.AddChatServices();

            _services.AddSingleton<MainWindow>();

            var serviceProvider = _services.BuildServiceProvider();

            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
                Console.WriteLine($"[UNHANDLED] {args.ExceptionObject}");   
            DispatcherUnhandledException += (_, args) =>
                Console.WriteLine($"[DISPATCHER]  {args.Exception}");

            var mainWindow = serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _services.DeleteChatService();
            ConsoleHelper.Detach();
            base.OnExit(e);
        }
    }

}
