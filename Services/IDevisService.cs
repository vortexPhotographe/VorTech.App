using System.Collections.Generic;
using VorTech.App.Models;

namespace VorTech.App.Services
{
    public interface IDevisService
    {
        // Devis (entête / lecture)
        Devis? GetById(int id);
        List<Devis> Search(string? q);              // si tu ne l’as pas, tu peux l’implémenter plus tard / ou la retirer d’ici

        // Création / suppression
        int CreateDraft();
        void SoftDelete(int devisId);

        // Champs client (snapshot)
        void UpdateClientFields(int devisId,
            string? societe,
            string? nomPrenom,
            string? adresse1,
            string? codePostal,
            string? ville,
            string? email,
            string? telephone);

        // Champs entête (titre, notes, remise)
        void UpdateHeaderFields(int devisId, string? titre, string? noteHaut, string? noteBas, decimal? remiseGlobale);
        void SetRemiseGlobale(int devisId, decimal remiseGlobale);

        // Banque
        void SetBankAccount(int devisId, int? bankAccountId);

        // Lignes
        List<DevisLigne> GetLines(int devisId);
        int AddLine(int devisId, string designation, decimal qty, decimal pu, int? articleId, int? variantId, string? imagePath);
        void UpdateLine(int lineId, string designation, decimal qty, decimal pu);
        void DeleteLine(int lineId);

        // Totaux
        void RecalcTotals(int devisId);

        // Modalités de règlement (SIMPLE/DUAL) — snapshot devis
        void SetPaymentTerms(int devisId, int? paymentTermsId);

        // Émission (numérotation + PDF)
        string Emit(int devisId, INumberingService numbering);
    }
}