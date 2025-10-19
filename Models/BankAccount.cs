namespace VorTech.App.Models
{
    public class BankAccount
    {
        public int Id { get; set; }
        public string? DisplayName { get; set; }
        public string? Iban { get; set; }
        public string? Bic { get; set; }
        public string? Holder { get; set; }
        public string? BankName { get; set; }
        public bool IsDefault { get; set; }
    }
}
