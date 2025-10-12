namespace VorTech.App.Models
{
    public class PackItem
    {
        public int Id { get; set; }
        public int ArticlePackId { get; set; }   // Id de l'article de type Pack
        public int ArticleItemId { get; set; }   // Id de l'article composant
        public double Quantite { get; set; }     // pas de réassort direct sur Pack
    }
}
