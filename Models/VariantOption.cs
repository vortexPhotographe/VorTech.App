namespace VorTech.App.Models
{
    // Axe de déclinaison (ex: Couleur, Taille, Connectique)
    public class VariantOption
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";   // ex: "Couleur"
        public string? Notes { get; set; }
    }

    // Valeur d'un axe (ex: Rouge, Noir ; S, M, L)
    public class VariantOptionValue
    {
        public int Id { get; set; }
        public int OptionId { get; set; }
        public string Value { get; set; } = "";
    }
}
