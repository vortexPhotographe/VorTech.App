using System;
using System.IO;
using System.Text.Json;
using VorTech.App.Models;

namespace VorTech.App.Services
{
    public static class ConfigService
    {
        private static readonly string Root = AppContext.BaseDirectory;
        private static readonly string ConfigDir = Path.Combine(Root, "Config");
        private static readonly string FilePath = Path.Combine(ConfigDir, "appsettings.json");

        public static AppConfig Load()
        {
            try
            {
                if (!Directory.Exists(ConfigDir)) Directory.CreateDirectory(ConfigDir);
                if (File.Exists(FilePath))
                {
                    var json = File.ReadAllText(FilePath);
                    var cfg = JsonSerializer.Deserialize<AppConfig>(
                        json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );
                    return cfg ?? new AppConfig();
                }
            }
            catch { /* on garde des valeurs par défaut */ }

            return new AppConfig();
        }

        public static void Save(AppConfig cfg)
        {
            if (!Directory.Exists(ConfigDir)) Directory.CreateDirectory(ConfigDir);
            var json = JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
    }
}
