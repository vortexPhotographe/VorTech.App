using System;

namespace VorTech.App.Models
{
    public class Article
    {
        public int Id { get; set; }

        // Clé "métier"
        public string Code { get; set; } = "";            // ex: "ART-0001"
        public string Libelle { get; set; } = "";
        public ArticleType Type { get; set; } = ArticleType.Article;

        // Références catalogues
        public int? CategorieId { get; set; }
        public int? TvaRateId { get; set; }          // si TVA activée
        public int? CotisationRateId { get; set; }   // si TVA désactivée

        // Prix
        public decimal PrixAchatHT { get; set; }
        public decimal PrixVenteHT { get; set; }

        // Stock & infos
        public decimal StockActuel { get; set; }
        public decimal SeuilAlerte { get; set; }
        public decimal PoidsG { get; set; }
        public bool Actif { get; set; } = true;

        // Code-barres (article de base)
        public string? CodeBarres { get; set; }

        // Dates
        public DateOnly DerniereMaj { get; set; } = DateOnly.FromDateTime(DateTime.Now);

        // Affichage uniquement : Prix conseillé (non stocké)
        // Formule validée : PventeHT = 2 * Achat / (1 - taux)
        // taux = TVA si IsTvaEnabled, sinon taux de cotisation
        public decimal GetPrixConseilleHT(bool isTvaEnabled, decimal tauxTva, decimal tauxCotisation)
        {
            decimal taux = isTvaEnabled ? tauxTva : tauxCotisation; // ex: 0.20 pour 20%
            if (taux >= 1m) return 0m;
            if (PrixAchatHT <= 0m) return 0m;
            return Math.Round(2m * PrixAchatHT / (1m - taux), 2);
        }
    }
}
