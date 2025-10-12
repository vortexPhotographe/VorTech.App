namespace VorTech.App.Models
{
    public class Article
    {
        public int Id { get; set; }
        public string? Sku { get; set; }     // Référence interne
        public string? Name { get; set; }    // Libellé
        public double PriceHT { get; set; }  // Prix de vente HT (simple v1)
        public double Stock { get; set; }    // Stock actuel
    }
}
