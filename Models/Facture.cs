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
        public DateTime? SentAt { get; set; }
        public DateTime? DeletedAt { get; set; }
        public bool D { get; set; } = false;             // ton drapeau 'D'
        public int? DevisSourceId { get; set; }
    }
}
