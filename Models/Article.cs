namespace VorTech.App.Models
{
    public class Article
    {
        public int Id { get; set; }

        // Champs principaux
        public string Code { get; set; } = "";
        public string Libelle { get; set; } = "";
        public string? Type { get; set; }   // "Article" | "Pack" (UI verrouillé)

        public double PrixAchatHT { get; set; }
        public double PrixVenteHT { get; set; }

        // Valeur TVA numérique (synchro avec TvaRateId quand on sauvegarde)
        public double TVA { get; set; }

        public double StockActuel { get; set; }
        public double SeuilAlerte { get; set; }
        public bool Actif { get; set; } = true;
        public string? DerniereMAJ { get; set; }

        // Mémo éventuel
        public double TauxCotisation { get; set; }

        // Liens vers catalogues (Réglages)
        public int? CotisationTypeId { get; set; }
        public int? TvaRateId { get; set; }

        // Barcode & logistique
        public string? Barcode { get; set; }
        public string BarcodeType { get; set; } = "CODE128";
        public double PoidsUnitaireGr { get; set; }

        // Aide UI
        public bool EstPack => string.Equals(Type, "Pack", System.StringComparison.OrdinalIgnoreCase);
    }

    // Elément d’un pack
    public class ArticleComponent
    {
        public int Id { get; set; }
        public int PackArticleId { get; set; }
        public int ComponentArticleId { get; set; }
        public int? ComponentVariantId { get; set; } // variante précise optionnelle
        public double Qte { get; set; }

        // Projections lecture
        public string? ComponentCode { get; set; }
        public string? ComponentLibelle { get; set; }
    }
}
