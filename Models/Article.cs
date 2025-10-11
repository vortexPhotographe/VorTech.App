namespace VorTech.App.Models
{
    public class Article
    {
        public int Id { get; set; }

        // Champs principaux
        public string Code { get; set; } = "";           // ex: SKU
        public string Libelle { get; set; } = "";
        public string? Type { get; set; }                 // ex: "Produit", "Service", "Pack"
        public double PrixAchatHT { get; set; }
        public double PrixVenteHT { get; set; }
        public double TVA { get; set; }                   // conservé même si micro => 0 en pratique
        public double StockActuel { get; set; }
        public double SeuilAlerte { get; set; }
        public bool Actif { get; set; } = true;
        public string? DerniereMAJ { get; set; }          // ISO 8601 (stock / prix / fiche)
        public double TauxCotisation { get; set; }        // frais URSSAF/RSI éventuels, non répercutés

        // Aide UI
        public bool EstPack => string.Equals(Type, "Pack", System.StringComparison.OrdinalIgnoreCase);
    }

    // Elément de composition de pack (Article parent -> composants)
    public class ArticleComponent
    {
        public int Id { get; set; }
        public int PackArticleId { get; set; }
        public int ComponentArticleId { get; set; }
        public double Qte { get; set; }

        // projection lecture
        public string? ComponentCode { get; set; }
        public string? ComponentLibelle { get; set; }
    }
}
