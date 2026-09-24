using System.Windows;
using GUI.Configuration;
using GUI.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using SimpleChat.ChatApplication;

namespace GUI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private ChatApplication? _chat;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var settings = StartupSettings.Load();
            if (settings.LaunchConsole)
            {
                ConsoleHelper.Attach();
                if (settings.HideConsoleOnStart) ConsoleHelper.Hide();
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Приложение запущено.");
            }

            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
                Console.WriteLine($"[UNHANDLED] {args.ExceptionObject}");
            DispatcherUnhandledException += (_, args) =>
                Console.WriteLine($"[DISPATCHER]  {args.Exception}");

            try
            {
                _chat = await ChatApplication.StartAsync(services =>
                {
                    services.AddSingleton<MainWindow>();
                });

                _chat.Services.GetRequiredService<MainWindow>().Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось запустить приложение: {ex.Message}",
                                "Ошибка запуска",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            if (_chat is not null)
            {
                try { await _chat.DisposeAsync(); }
                catch (Exception ex) { Console.WriteLine($"[EXIT] {ex}"); }
            }
            base.OnExit(e);
        }
    }

}
