using System;
using System.IO;
using System.Runtime.InteropServices;

namespace GUI.Infrastructure
{
    /// <summary>
    /// Управляет консолью процесса. 
    /// </summary>
    internal static class ConsoleHelper
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeConsole();

        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleOutputCP(uint wCodePageID);

        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleCP(uint wCodePageID);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;

        private const uint CP_UTF8 = 65001;

        public static void Attach()
        {
            if (GetConsoleWindow() != IntPtr.Zero) return;
            if (!AllocConsole()) return;

            SetConsoleOutputCP(CP_UTF8);
            SetConsoleCP(CP_UTF8);

            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.InputEncoding = System.Text.Encoding.UTF8;

            var stdout = Console.OpenStandardOutput();
            Console.SetOut(new StreamWriter(stdout) { AutoFlush = true });

            var stderr = Console.OpenStandardError();
            Console.SetError(new StreamWriter(stderr) { AutoFlush = true });

            var stdin = Console.OpenStandardInput();
            Console.SetIn(new StreamReader(stdin));
        }

        public static void Detach()
        {
            if (GetConsoleWindow() != IntPtr.Zero)
                FreeConsole();
        }

        public static void Show() => ShowWindow(GetConsoleWindow(), SW_SHOW);
        public static void Hide() => ShowWindow(GetConsoleWindow(), SW_HIDE);
    }
}