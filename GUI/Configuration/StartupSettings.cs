using System.IO;
using System.Text.Json;

namespace GUI.Configuration
{
    public class StartupSettings
    {
        public bool LaunchConsole { get; set; }
        public bool HideConsoleOnStart { get; set; }

        public static StartupSettings Load(string path = "startup.json")
        {
            if (!File.Exists(path))
                return new StartupSettings();   

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<StartupSettings>(json)
                   ?? new StartupSettings();
        }
    }
}