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

            var services = new ServiceCollection();

            services.AddChatServices();

            services.AddSingleton<MainWindow>();

            var serviceProvider = services.BuildServiceProvider();

            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
                Console.WriteLine($"[UNHANDLED] {args.ExceptionObject}");   
            DispatcherUnhandledException += (_, args) =>
                Console.WriteLine($"[DISPATCHER]  {args.Exception}");

            var mainWindow = serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            ConsoleHelper.Detach();
            base.OnExit(e);
        }
    }

}
