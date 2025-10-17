namespace VorTech.App.Models
{
    public class CompanyProfile
    {
        public int Id { get; set; } = 1;   // unique (1 seule ligne)
        public string NomCommercial { get; set; } = "";
        public string Siret { get; set; } = "";
        public string Adresse1 { get; set; } = "";
        public string Adresse2 { get; set; } = "";
        public string CodePostal { get; set; } = "";
        public string Ville { get; set; } = "";
        public string Pays { get; set; } = "";
        public string Email { get; set; } = "";
        public string Telephone { get; set; } = "";
        public string SiteWeb { get; set; } = "";
    }
}
