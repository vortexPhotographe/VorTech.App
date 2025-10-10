namespace VorTech.App.Models
{
    public class AppConfig
    {
        public bool IsMicro { get; set; } = true;

        public string? BusinessName { get; set; }
        public string? Siret { get; set; }
        public string? Iban { get; set; }
        public string? Bic { get; set; }
    }
}
