namespace VorTech.App.Models
{
    public class PackItem
    {
        public int Id { get; set; }
        public int PackArticleId { get; set; }      // l’article qui EST le pack
        public int ComponentArticleId { get; set; } // l’article composant
        public double Quantite { get; set; } = 1;
    }
}
