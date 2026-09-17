using System;
using System.IO;
using System.Text;

namespace ValheimAdminOverlay
{
    internal static class Log
    {
        private static string _file;

        // В точке входа Doorstop игровой цикл Unity ещё не поднят, и обращение к
        // UnityEngine.Debug.Log падает с MissingMethodException на icall-обёртке.
        // Поэтому в Unity пишем только после того, как хост успешно создан.
        internal static bool UnityAvailable;

        internal static void Init(string directory)
        {
            try
            {
                _file = Path.Combine(directory, "adminoverlay.log");
                File.WriteAllText(_file, $"=== {DateTime.Now:yyyy-MM-dd HH:mm:ss} ==={Environment.NewLine}", Encoding.UTF8);
            }
            catch (Exception)
            {
                _file = null;
            }
        }

        internal static void Info(string message) => Write("INFO", message);
        internal static void Warn(string message) => Write("WARN", message);
        internal static void Error(string message) => Write("ERROR", message);

        private static void Write(string level, string message)
        {
            var line = $"[{DateTime.Now:HH:mm:ss}] [{level}] {message}";

            if (UnityAvailable)
            {
                try
                {
                    UnityEngine.Debug.Log("[AdminOverlay] " + line);
                }
                catch (Exception)
                {
                    UnityAvailable = false;
                }
            }

            if (_file == null) return;

            try
            {
                File.AppendAllText(_file, line + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception)
            {
                _file = null;
            }
        }
    }
}
