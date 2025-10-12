namespace VorTech.App.Models
{
    public class ArticleVariant
    {
        public int Id { get; set; }
        public int ArticleId { get; set; }

        public string Nom { get; set; } = "";
        public double PrixHT { get; set; }
        public string CodeBarres { get; set; } = "";
    }
}
