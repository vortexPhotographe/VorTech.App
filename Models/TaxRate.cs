namespace VorTech.App.Models
{
    public class TaxRate
    {
        public int Id { get; set; }
        public string Nom { get; set; } = "";   // "TVA 20%"
        public double Taux { get; set; }        // 0.20
        public bool Actif { get; set; } = true;
    }
}
