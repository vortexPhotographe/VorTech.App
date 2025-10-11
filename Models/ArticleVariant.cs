namespace VorTech.App.Models
{
    // Variante concrète (combinaison d'axes/valeurs)
    public class ArticleVariant
    {
        public int Id { get; set; }
        public int ArticleId { get; set; }

        public string Code { get; set; } = "";        // SKU/Code unique variante
        public string? Barcode { get; set; }          // généré auto si vide
        public string BarcodeType { get; set; } = "CODE128";

        public double? PrixAchatHT { get; set; }      // surcharge éventuelle
        public double? PrixVenteHT { get; set; }      // surcharge éventuelle
        public double StockActuel { get; set; }
        public double PoidsUnitaireGr { get; set; }
        public bool Actif { get; set; } = true;
    }

    // Sélection des valeurs d’axes pour une variante
    public class ArticleVariantSelection
    {
        public int VariantId { get; set; }
        public int OptionId { get; set; }
        public int ValueId { get; set; }
    }
}
