namespace VorTech.App.Models
{
    public class CotisationRate
    {
        public int Id { get; set; }
        public string Nom { get; set; } = "";   // ex : "URSSAF 22%"
        public double Taux { get; set; }        // ex : 0.22
    }
}
