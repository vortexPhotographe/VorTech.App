namespace VorTech.App.Models
{
    public class PaymentMode
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public decimal FeeFixed { get; set; }   // €
        public decimal FeeRate { get; set; }    // %
        public bool IsActive { get; set; }
        public override string ToString() => Name ?? "";
    }
}
