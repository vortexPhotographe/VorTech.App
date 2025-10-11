namespace VorTech.App.Models
{
    public class CotisationType
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";   // ex: "Micro - vente de biens"
        public bool Liberatoire { get; set; }    // versement libératoire ?
        public double Rate { get; set; }         // taux en %
        public string? Notes { get; set; }
    }
}
