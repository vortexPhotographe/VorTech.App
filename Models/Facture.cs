using System;

namespace VorTech.App.Models
{
    public class Facture
    {
        public int Id { get; set; }
        public string? Numero { get; set; }
        public int? ClientId { get; set; }
        public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);
        public string Etat { get; set; } = "Brouillon";  // Brouillon/Cree/Envoye/Payer
        public decimal RemiseGlobale { get; set; }
        public decimal Total { get; set; }
        public int? PaymentTermsId { get; set; }
        public int? BankAccountId { get; set; }          // <-- utilisé par le pied de page PDF

        // ---- SNAPSHOT BANCK ----
        public string? BankHolder { get; set; }
        public string? BankName { get; set; }
        public string? Iban { get; set; }
        public string? Bic { get; set; }

        // ---- SNAPSHOT DESTINATAIRE (comme Devis) ----
        public string? ClientSociete { get; set; }
        public string? ClientNomPrenom { get; set; }
        public string? ClientAdresseL1 { get; set; }
        public string? ClientCodePostal { get; set; }
        public string? ClientVille { get; set; }
        public string? ClientEmail { get; set; }
        public string? ClientTelephone { get; set; }

        // ---- métadonnées ----
        public DateTime? SentAt { get; set; }
        public DateTime? DeletedAt { get; set; }
        public bool D { get; set; } = false;
        public int? DevisSourceId { get; set; }
    }
}
