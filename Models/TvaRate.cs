namespace VorTech.App.Models
{
    public class TvaRate
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";   // "0%", "Taux normal", etc.
        public double Rate { get; set; }         // 0, 20, 10, 5.5, 2.1
        public bool IsDefault { get; set; }      // par ex. 0% pour micro
    }
}
