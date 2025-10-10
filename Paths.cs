using System;
using System.IO;

namespace VorTech.App
{
    public static class Paths
    {
        public static string AppDir => AppContext.BaseDirectory;
        public static string DataDir => Path.Combine(AppDir, "Data");
        public static string AssetsDir => Path.Combine(AppDir, "Assets");
        public static string LogsDir => Path.Combine(AppDir, "Logs");
        public static string DbPath => Path.Combine(DataDir, "app.db");
        static Paths()
        {
            Directory.CreateDirectory(DataDir);
            Directory.CreateDirectory(AssetsDir);
            Directory.CreateDirectory(LogsDir);
        }
    }
}
