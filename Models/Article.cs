using System;

namespace VorTech.App.Models
{
    public enum ArticleType { Article = 0, Pack = 1 }

    public class Article
    {
        public int Id { get; set; }
        public string Reference { get; set; } = "";
        public string Libelle  { get; set; } = "";
        public ArticleType Type { get; set; } = ArticleType.Article;
        public int? CategoryId { get; set; }

        public double PrixAchatHT { get; set; }
        public double PrixVenteHT { get; set; }

        public int StockActuel { get; set; }
        public int SeuilAlerte { get; set; }

        public double Poids_g { get; set; }
        public bool Actif { get; set; } = true;

        public string CodeBarres { get; set; } = "";
        public string? Description { get; set; }

        public DateTime DateMaj { get; set; } = DateTime.UtcNow;
    }
}
