using System;

namespace VorTech.App.Models
{
    public class Client
    {
		public int Id { get; set; }

        // obligatoires (par défaut vides pour éviter les null)
        public string Nom { get; set; } = "";
        public string Prenom { get; set; } = "";

        // optionnels (peuvent être null)
        public string? NomTeam { get; set; }
        public string? Societe { get; set; }
        public string? Siret { get; set; }
        public string? Adresse { get; set; }
        public string? CodePostal { get; set; }
        public string? Ville { get; set; }
        public string? Email { get; set; }
        public string? Telephone { get; set; }
        public string? Notes { get; set; }

        // Compat pour ancien code/colonnes (non lie a l'UI)
        public string Name => (Prenom + " " + Nom).Trim();
    }
}