using System.Collections.ObjectModel;

namespace VorTech.App.Models
{
    public class AppConfig
    {
        public bool IsMicro { get; set; }

        public string? Siret { get; set; }
        public string? Iban  { get; set; }
        public string? Bic   { get; set; }
        public string? BusinessName { get; set; }

        public ObservableCollection<PaymentMethod> PaymentMethods { get; set; } = new();
    }

    public class PaymentMethod
    {
        public string? Name { get; set; }
        public double FixedFee { get; set; }     // en €
        public double PercentFee { get; set; }   // en %
    }
}
