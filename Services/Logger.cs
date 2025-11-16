using System;
using System.IO;

namespace VorTech.App
{
    public static class Logger
    {
        private static readonly object _sync = new();
        private static string _logFile = Path.Combine(Paths.LogsDir, "app.log");
        private static bool _initialized = true;

        static Logger() { /* on ne fait rien ici, on laisse Init gérer le dossier */ }

        public static void Init(string? customLogsDir = null)
        {
            try
            {
                var dir = string.IsNullOrWhiteSpace(customLogsDir) ? Paths.LogsDir : customLogsDir;
                Directory.CreateDirectory(dir);
                _logFile = Path.Combine(dir, "app.log");
                _initialized = true;
                WriteLine("=== APP START ===");
            }
            catch
            {
                // On ne jette pas d’exception au démarrage pour le log
            }
        }

        public static void Info(string message) => WriteLine("[INFO] " + message);

        public static void Error(string message, Exception? ex = null) =>
            WriteLine("[ERR ] " + message + (ex != null ? " :: " + ex : ""));

        private static void WriteLine(string line)
        {
            if (!_initialized)
            {
                // fallback: tenter de créer le dossier (premier usage direct sans Init)
                try
                {
                    Directory.CreateDirectory(Paths.LogsDir);
                    _logFile = Path.Combine(Paths.LogsDir, "app.log");
                    _initialized = true;
                }
                catch { /* ignore */ }
            }

            try
            {
                lock (_sync)
                {
                    File.AppendAllText(
                        _logFile,
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {line}{Environment.NewLine}"
                    );
                }
            }
            catch { /* ne bloque jamais l’app à cause du log */ }
        }
    }
}
