namespace VorTech.App.Models
{
    public class ArticleReassort
    {
        public int Id { get; set; }
        public int ArticleId { get; set; }
        public string Date { get; set; } = "";       // ISO 8601
        public string? Fournisseur { get; set; }
        public double Qte { get; set; }
        public double PUAchatHT { get; set; }
        public string? Notes { get; set; }
    }
}
