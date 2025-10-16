// Services/ConfigService.cs
using System;
using System.IO;
using System.Text.Json;
using VorTech.App.Models; // <-- utilise le modèle unique

namespace VorTech.App.Services
{
    public static class ConfigService
    {
        private static readonly string ConfigPath = Path.Combine(Paths.DataDir, "config.json");
        private static AppConfig? _cache;

        // Compat: certains fichiers appellent Load(), d'autres Get()
        public static AppConfig Load() => Get();

        public static AppConfig Get()
        {
            if (_cache != null) return _cache;

            Directory.CreateDirectory(Paths.DataDir);

            if (!File.Exists(ConfigPath))
            {
                var def = new AppConfig();   // modèle de Models/AppConfig.cs
                Save(def);
                _cache = def;
                return def;
            }

            var json = File.ReadAllText(ConfigPath);
            _cache = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            if (string.IsNullOrWhiteSpace(_cache!.TaxMode))
                _cache!.TaxMode = _cache!.IsMicro ? "Micro" : "TVA";
            return _cache!;
        }

        public static void Save(AppConfig cfg)
        {
            Directory.CreateDirectory(Paths.DataDir);
            var json = JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
            _cache = cfg;
        }
    }
}
