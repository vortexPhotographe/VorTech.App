namespace VorTech.App.Models
{
    public class FactureLigne
    {
        public int Id { get; set; }
        public int FactureId { get; set; }
        public string Designation { get; set; } = "";
        public decimal Qty { get; set; } = 1m;
        public decimal PU { get; set; } = 0m;
        public decimal Montant { get; set; } = 0m;
        public int? ArticleId { get; set; }
        public int? VarianteId { get; set; }
        public int? CotisationRateId { get; set; }
        public int? DevisLigneId { get; set; }
    }
}
