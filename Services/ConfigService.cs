using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using VorTech.App.Models;

namespace VorTech.App.Services
{
    public static class ConfigService
    {
        // Si tu as déjà un Paths.ConfigJson garde-le; sinon mets un chemin relatif
        private static string ConfigPath =>
            Paths.ConfigJson; // ex: Path.Combine(AppContext.BaseDirectory, "Config", "appsettings.json")

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static AppConfig Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    var cfg = JsonSerializer.Deserialize<AppConfig>(json, JsonOpts);
                    return cfg ?? new AppConfig();
                }
            }
            catch { /* log si tu veux */ }

            // Valeurs par défaut
            return new AppConfig
            {
                PaymentMethods =
                {
                    new PaymentMethod { Name = "CB",      FixedFee = 0.25, PercentFee = 1.5 },
                    new PaymentMethod { Name = "Espèces", FixedFee = 0,    PercentFee = 0   },
                    new PaymentMethod { Name = "PayPal",  FixedFee = 0.35, PercentFee = 2.9 },
                    new PaymentMethod { Name = "Virement",FixedFee = 0,    PercentFee = 0   }
                }
            };
        }

        public static void Save(AppConfig config)
        {
            var dir = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(config, JsonOpts);
            File.WriteAllText(ConfigPath, json);
        }
    }
}
