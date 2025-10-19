namespace VorTech.App.Models
{
    public class Devis
    {
        public int Id { get; set; }
        public string? Numero { get; set; }                 // null tant que Brouillon
        public DateOnly Date { get; set; }
        public string Etat { get; set; } = "Brouillon";     // Brouillon|Envoye|Transforme|Expire

        public int? ClientId { get; set; }                  // lien durable vers fiche client
        // snapshot
        public string? ClientNom { get; set; }
        public string? ClientEmail { get; set; }
        public string? ClientTelephone { get; set; }
        public string? ClientAdresseL1 { get; set; }
        public string? ClientCodePostal { get; set; }
        public string? ClientVille { get; set; }

        public string? NoteHaut { get; set; }
        public string? NoteBas { get; set; }
        public string? ClientSociete { get; set; }
        public string? ClientNomPrenom { get; set; }

        public decimal RemiseGlobale { get; set; }
        public decimal Total { get; set; }

        public DateTime? DeletedAt { get; set; }
        
    }

    public class DevisLigne
    {
        public int Id { get; set; }
        public int DevisId { get; set; }
        public string Designation { get; set; } = "";
        public decimal Qty { get; set; }
        public decimal PU { get; set; }
        public decimal Montant { get; set; }
        public int? ArticleId { get; set; }
        public int? VarianteId { get; set; }
        public string? ImagePath { get; set; }
    }

    public class DevisAnnexe
    {
        public int Id { get; set; }
        public int DevisId { get; set; }
        public string Type { get; set; } = "PDF";  // PDF|PLANCHE
        public string? Chemin { get; set; }        // relatif (Assets/Annexes/...)
        public int Ordre { get; set; }
        public string? ConfigJson { get; set; }
    }
}
