namespace VorTech.App.Models
{
    public class PaymentTerm
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Mode { get; set; } = "SIMPLE";        // SIMPLE | DUAL
        public string? SimpleDue { get; set; }              // AT_ORDER | AT_DELIVERY (si SIMPLE)
        public double? OrderPct { get; set; }               // 0..100 (si DUAL)
        public bool IsDefault { get; set; }
        public string Body { get; set; } = "";              // aperçu lisible
    }
}
