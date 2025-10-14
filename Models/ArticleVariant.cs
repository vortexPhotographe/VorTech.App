namespace VorTech.App.Models
{
    public class ArticleVariant
    {
        public int Id { get; set; }
        public int ArticleId { get; set; }
        public string Nom { get; set; } = "";        // ex: "Couleur Rouge / T38"
        public decimal PrixVenteHT { get; set; }
        public string? CodeBarres { get; set; }      // généré à partir du préfixe + checksum
        public string? ImagePath { get; set; }       // chemin relatif ou null
        public decimal PrixAchatHT { get; set; }
        public decimal StockActuel { get; set; }
        public decimal SeuilAlerte { get; set; }
    }
}
