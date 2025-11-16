using DocumentFormat.OpenXml.Office2010.Excel;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using VorTech.App;
using VorTech.App.Models;
using VorTech.App.Services;


namespace VorTech.App.Services
{
    public class FactureService
    {
        private readonly ArticleService _articles = new ArticleService();

        // --- CRUD entête ---
        public int CreateDraft(int? clientId, int? fromDevisId = null)
        {
            using var cn = Db.Open();
            using var tx = cn.BeginTransaction();

            // 1) Créer la facture (brouillon) et récupérer son Id
            int id;
            using (var cmd = cn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
INSERT INTO Factures
    (Numero, ClientId, Date, Etat, RemiseGlobale, Total, PaymentTermsId, D, DevisSourceId)
VALUES
    (NULL,   @c,       DATE('now'), 'Brouillon', 0,     0,     NULL,           0, @ds);
SELECT last_insert_rowid();";
                Db.AddParam(cmd, "@c", (object?)clientId ?? DBNull.Value);
                Db.AddParam(cmd, "@ds", (object?)fromDevisId ?? DBNull.Value);

                id = Convert.ToInt32(cmd.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
            }

            // 2) Chercher le compte bancaire par défaut (BankAccounts.IsDefault = 1)
            int? bankId = null;
            string? holder = null, bankName = null, iban = null, bic = null;

            using (var cmdb = cn.CreateCommand())
            {
                cmdb.Transaction = tx;
                cmdb.CommandText = @"
SELECT Id, Holder, BankName, Iban, Bic
FROM BankAccounts
WHERE IsDefault = 1
LIMIT 1;";
                using var rd = cmdb.ExecuteReader();
                if (rd.Read())
                {
                    bankId = Convert.ToInt32(rd["Id"]);
                    holder = rd["Holder"]?.ToString();
                    bankName = rd["BankName"]?.ToString();
                    iban = rd["Iban"]?.ToString();
                    bic = rd["Bic"]?.ToString();
                }
            }

            // 3) Si un compte par défaut existe, on l’associe + on snapshotte dans la facture
            if (bankId.HasValue)
            {
                using var up = cn.CreateCommand();
                up.Transaction = tx;
                up.CommandText = @"
UPDATE Factures
SET BankAccountId = @bid,
    BankHolder    = @bh,
    BankName      = @bn,
    Iban          = @iban,
    Bic           = @bic
WHERE Id = @id;";
                Db.AddParam(up, "@bid", bankId.Value);
                Db.AddParam(up, "@bh", (object?)holder ?? DBNull.Value);
                Db.AddParam(up, "@bn", (object?)bankName ?? DBNull.Value);
                Db.AddParam(up, "@iban", (object?)iban ?? DBNull.Value);
                Db.AddParam(up, "@bic", (object?)bic ?? DBNull.Value);
                Db.AddParam(up, "@id", id);
                up.ExecuteNonQuery();
            }

            tx.Commit();
            return id;
        }

        public Facture? GetById(int id)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
SELECT
    Id,
    Numero,
    Etat,
    Date,
    ClientId,
    ClientSociete,
    ClientNomPrenom,
    ClientAdresseL1,
    ClientCodePostal,
    ClientVille,
    ClientEmail,
    ClientTelephone,
    RemiseGlobale,
    Total,
    PaymentTermsId,
    BankAccountId,
    DevisSourceId,
    BankHolder,
    BankName,
    Iban,
    Bic
FROM Factures
WHERE Id=@id;";
            Db.AddParam(cmd, "@id", id);

            using var rd = cmd.ExecuteReader();
            if (!rd.Read()) return null;

            return new Facture
            {
                Id = Convert.ToInt32(rd["Id"]),
                Numero = rd["Numero"]?.ToString(),
                Etat = rd["Etat"]?.ToString() ?? "Brouillon",
                Date = rd["Date"] is DBNull ? DateOnly.FromDateTime(DateTime.Now)
                                            : DateOnly.Parse(rd["Date"]!.ToString()!),

                ClientId = rd["ClientId"] is DBNull ? null : Convert.ToInt32(rd["ClientId"]),
                ClientSociete = rd["ClientSociete"]?.ToString(),
                ClientNomPrenom = rd["ClientNomPrenom"]?.ToString(),
                ClientAdresseL1 = rd["ClientAdresseL1"]?.ToString(),
                ClientCodePostal = rd["ClientCodePostal"]?.ToString(),
                ClientVille = rd["ClientVille"]?.ToString(),
                ClientEmail = rd["ClientEmail"]?.ToString(),
                ClientTelephone = rd["ClientTelephone"]?.ToString(),
                BankHolder = rd["BankHolder"] as string,
                BankName = rd["BankName"] as string,
                Iban = rd["Iban"] as string,
                Bic = rd["Bic"] as string,
                RemiseGlobale = rd["RemiseGlobale"] is DBNull ? 0m : Convert.ToDecimal(rd["RemiseGlobale"]),
                Total = rd["Total"] is DBNull ? 0m : Convert.ToDecimal(rd["Total"]),

                PaymentTermsId = rd["PaymentTermsId"] is DBNull ? (int?)null : Convert.ToInt32(rd["PaymentTermsId"]),
                BankAccountId = rd["BankAccountId"] is DBNull ? (int?)null : Convert.ToInt32(rd["BankAccountId"]),
                DevisSourceId = rd["DevisSourceId"] is DBNull ? (int?)null : Convert.ToInt32(rd["DevisSourceId"]),
            };
        }

        private static DateTime? TryParseDateTime(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (DateTime.TryParse(s, out var dt)) return dt;
            return null;
        }

        // --- Lignes ---
        public List<FactureLigne> GetLines(int factureId)
        {
            var list = new List<FactureLigne>();
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT * FROM FactureLignes WHERE FactureId=@f ORDER BY Id;";
            Db.AddParam(cmd, "@f", factureId);
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                list.Add(new FactureLigne
                {
                    Id = Convert.ToInt32(rd["Id"]),
                    FactureId = Convert.ToInt32(rd["FactureId"]),
                    Designation = rd["Designation"]?.ToString() ?? "",
                    Qty = Convert.ToDecimal(rd["Qty"] ?? 1m, CultureInfo.InvariantCulture),
                    PU = Convert.ToDecimal(rd["PU"] ?? 0m, CultureInfo.InvariantCulture),
                    Montant = Convert.ToDecimal(rd["Montant"] ?? 0m, CultureInfo.InvariantCulture),
                    ArticleId = rd["ArticleId"] as int?,
                    VarianteId = rd["VarianteId"] as int?,
                    CotisationRateId = rd["CotisationRateId"] as int?,
                    DevisLigneId = rd["DevisLigneId"] as int?
                });
            }
            return list;
        }

        public int AddLine(int factureId, string designation, decimal qty, decimal pu,
                           int? articleId = null, int? varianteId = null,
                           int? cotisationRateId = null, int? devisLigneId = null)
        {
            if (qty < 0m || pu < 0m) throw new InvalidOperationException("Valeurs négatives interdites");
            var montant = qty * pu;

            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
INSERT INTO FactureLignes(FactureId,Designation,Qty,PU,Montant,ArticleId,VarianteId,CotisationRateId,DevisLigneId)
VALUES(@f,@lib,@q,@pu,@m,@aid,@vid,@cr,@dl);
SELECT last_insert_rowid();";
            Db.AddParam(cmd, "@f", factureId);
            Db.AddParam(cmd, "@lib", designation ?? "");
            Db.AddParam(cmd, "@q", qty);
            Db.AddParam(cmd, "@pu", pu);
            Db.AddParam(cmd, "@m", montant < 0m ? 0m : montant);
            Db.AddParam(cmd, "@aid", (object?)articleId ?? DBNull.Value);
            Db.AddParam(cmd, "@vid", (object?)varianteId ?? DBNull.Value);
            Db.AddParam(cmd, "@cr", (object?)cotisationRateId ?? DBNull.Value);
            Db.AddParam(cmd, "@dl", (object?)devisLigneId ?? DBNull.Value);
            var id = Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);

            RecalcTotals(factureId);
            return id;
        }

        public void UpdateLine(int ligneId, string designation, decimal qty, decimal pu)
        {
            if (qty < 0m || pu < 0m) throw new InvalidOperationException("Valeurs négatives interdites");

            using var cn = Db.Open();

            int factureId;
            using (var get = cn.CreateCommand())
            {
                get.CommandText = "SELECT FactureId FROM FactureLignes WHERE Id=@id;";
                Db.AddParam(get, "@id", ligneId);
                factureId = Convert.ToInt32(get.ExecuteScalar() ?? 0);
                if (factureId == 0) return;
            }

            var m = qty * pu;
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = "UPDATE FactureLignes SET Designation=@lib, Qty=@q, PU=@pu, Montant=@m WHERE Id=@id;";
                Db.AddParam(cmd, "@lib", designation ?? "");
                Db.AddParam(cmd, "@q", qty);
                Db.AddParam(cmd, "@pu", pu);
                Db.AddParam(cmd, "@m", m < 0m ? 0m : m);
                Db.AddParam(cmd, "@id", ligneId);
                cmd.ExecuteNonQuery();
            }

            RecalcTotals(factureId);
        }

        public void DeleteLine(int ligneId)
        {
            using var cn = Db.Open();

            int factureId;
            using (var get = cn.CreateCommand())
            {
                get.CommandText = "SELECT FactureId FROM FactureLignes WHERE Id=@id;";
                Db.AddParam(get, "@id", ligneId);
                factureId = Convert.ToInt32(get.ExecuteScalar() ?? 0);
                if (factureId == 0) return;
            }

            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM FactureLignes WHERE Id=@id;";
                Db.AddParam(cmd, "@id", ligneId);
                cmd.ExecuteNonQuery();
            }

            RecalcTotals(factureId);
        }

        public void RecalcTotals(int factureId)
        {
            using var cn = Db.Open();

            decimal lignes = 0m;
            using (var sum = cn.CreateCommand())
            {
                sum.CommandText = "SELECT COALESCE(SUM(Montant),0) FROM FactureLignes WHERE FactureId=@f;";
                Db.AddParam(sum, "@f", factureId);
                lignes = Convert.ToDecimal(sum.ExecuteScalar() ?? 0m, CultureInfo.InvariantCulture);
            }

            decimal remise;
            using (var get = cn.CreateCommand())
            {
                get.CommandText = "SELECT RemiseGlobale FROM Factures WHERE Id=@f;";
                Db.AddParam(get, "@f", factureId);
                remise = Convert.ToDecimal(get.ExecuteScalar() ?? 0m, CultureInfo.InvariantCulture);
            }

            var total = lignes - remise;
            if (total < 0m) total = 0m;

            using (var upd = cn.CreateCommand())
            {
                upd.CommandText = "UPDATE Factures SET Total=@t WHERE Id=@f;";
                Db.AddParam(upd, "@t", total);
                Db.AddParam(upd, "@f", factureId);
                upd.ExecuteNonQuery();
            }
        }

        public string EmitNumero(int factureId, INumberingService num)
        {
            Logger.Info($"FACTURE EmitNumero: start id={factureId}");

            using var cn = Db.Open();

            // 1) Lire sans transaction : déjà numérotée ?
            string? currentNumero = null;
            DateOnly docDate = DateOnly.FromDateTime(DateTime.Today);

            using (var check = cn.CreateCommand())
            {
                check.CommandText = "SELECT Numero, Date FROM Factures WHERE Id=@id;";
                Db.AddParam(check, "@id", factureId);

                using var rd = check.ExecuteReader();
                if (!rd.Read())
                    throw new InvalidOperationException("Facture introuvable.");

                currentNumero = rd.IsDBNull(0) ? null : rd.GetString(0);

                if (!rd.IsDBNull(1))
                {
                    var s = rd.GetValue(1)?.ToString();
                    if (!string.IsNullOrWhiteSpace(s) && DateOnly.TryParse(s, out var d))
                        docDate = d;
                }
            }

            if (!string.IsNullOrWhiteSpace(currentNumero))
            {
                Logger.Info($"FACTURE EmitNumero: déjà numérotée -> {currentNumero}");
                return currentNumero!;
            }

            // 2) Réserver le numéro AVANT toute transaction ici (évite le lock croisé)
            Logger.Info($"FACTURE EmitNumero: réserve numéro (docType=FACT, date={docDate})");
            var reserved = num.Next("FACT", docDate);
            Logger.Info($"FACTURE EmitNumero: numéro réservé = {reserved}");

            // 3) Écrire le numéro dans la facture (petite TX locale)
            using var tx = cn.BeginTransaction();
            try
            {
                // garde-fou : ne pose le n° que si toujours vide
                using (var upd = cn.CreateCommand())
                {
                    upd.Transaction = tx;
                    upd.CommandText = @"
UPDATE Factures
   SET Numero = @n,
       Etat   = 'Cree',
       Date   = DATE('now')
 WHERE Id = @id
   AND (Numero IS NULL OR TRIM(Numero)='');";
                    Db.AddParam(upd, "@n", reserved);
                    Db.AddParam(upd, "@id", factureId);
                    var rows = upd.ExecuteNonQuery();
                    Logger.Info($"FACTURE EmitNumero: UPDATE rows={rows}");
                }

                // si quelqu’un l’a numérotée entre-temps, on relit pour renvoyer la vraie valeur
                string finalNumero = reserved;
                using (var re = cn.CreateCommand())
                {
                    re.Transaction = tx;
                    re.CommandText = "SELECT Numero FROM Factures WHERE Id=@id;";
                    Db.AddParam(re, "@id", factureId);
                    finalNumero = Convert.ToString(re.ExecuteScalar()) ?? reserved;
                }

                tx.Commit();
                Logger.Info("FACTURE EmitNumero: commit OK");
                return finalNumero;
            }
            catch (Exception ex)
            {
                Logger.Error("FACTURE EmitNumero: échec, rollback", ex);
                try { tx.Rollback(); } catch { /* ignore */ }
                throw;
            }
        }


        // Création facture depuis un devis clients
        public int CreateFromDevis(int devisId)
        {
            // 1) lire le devis + ses lignes (hors transaction)
            var devSvc = new DevisService();
            var d = devSvc.GetById(devisId) ?? throw new InvalidOperationException("Devis introuvable.");
            var dLines = devSvc.GetLines(devisId);

            using var cn = Db.Open();
            using var tx = cn.BeginTransaction();

            // 2) créer le brouillon de facture (dans la transaction)
            int facId;
            using (var cmd = cn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
INSERT INTO Factures(Numero, ClientId, Date, Etat, RemiseGlobale, Total, PaymentTermsId, D, DevisSourceId)
VALUES(NULL, @c, DATE('now'), 'Brouillon', 0, 0, NULL, 0, @ds);
SELECT last_insert_rowid();";
                Db.AddParam(cmd, "@c", (object?)d.ClientId ?? DBNull.Value);
                Db.AddParam(cmd, "@ds", devisId);
                facId = Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
            }

            // 3) snapshot client + remise + modalités
            using (var up = cn.CreateCommand())
            {
                up.Transaction = tx;
                up.CommandText = @"
UPDATE Factures SET
    ClientSociete    = @cs,
    ClientNomPrenom  = @cnp,
    ClientAdresseL1  = @cadr,
    ClientCodePostal = @cp,
    ClientVille      = @cv,
    ClientEmail      = @ce,
    ClientTelephone  = @ctel,
    PaymentTermsId   = @pt,
    RemiseGlobale    = @rem
WHERE Id=@id;";
                Db.AddParam(up, "@cs", d.ClientSociete);
                Db.AddParam(up, "@cnp", d.ClientNomPrenom);
                Db.AddParam(up, "@cadr", d.ClientAdresseL1);
                Db.AddParam(up, "@cp", d.ClientCodePostal);
                Db.AddParam(up, "@cv", d.ClientVille);
                Db.AddParam(up, "@ce", d.ClientEmail);
                Db.AddParam(up, "@ctel", d.ClientTelephone);
                Db.AddParam(up, "@pt", (object?)d.PaymentTermsId ?? DBNull.Value);
                Db.AddParam(up, "@rem", d.RemiseGlobale);
                Db.AddParam(up, "@id", facId);
                up.ExecuteNonQuery();
            }

            // 4) copier les lignes (toujours dans le MÊME cn/tx)
            using (var ins = cn.CreateCommand())
            {
                ins.Transaction = tx;
                ins.CommandText = @"
INSERT INTO FactureLignes(FactureId,Designation,Qty,PU,Montant,ArticleId,VarianteId,CotisationRateId,DevisLigneId)
VALUES(@f,@lib,@q,@pu,@m,@aid,@vid,@cr,@dl);";

                foreach (var l in dLines)
                {
                    // déterminer le taux de cotisation depuis article/variante (lookup en lecture → ok)
                    int? cotRateId = null;
                    if (l.ArticleId is int aid)
                    {
                        var a = _articles.GetById(aid);
                        cotRateId = a?.CotisationRateId;

                        if (l.VarianteId is int vid)
                        {
                            var v = _articles.GetVariantById(vid);
                            // ArticleVariant n’a pas CotisationRateId → on garde celui de l’article
                            // (si un jour tu l’ajoutes à la variante, dé-commente la ligne suivante)
                            // cotRateId = v?.CotisationRateId ?? cotRateId;
                        }
                    }

                    ins.Parameters.Clear();
                    Db.AddParam(ins, "@f", facId);
                    Db.AddParam(ins, "@lib", l.Designation ?? "");
                    Db.AddParam(ins, "@q", l.Qty);
                    Db.AddParam(ins, "@pu", l.PU);
                    Db.AddParam(ins, "@m", l.Qty * l.PU);
                    Db.AddParam(ins, "@aid", (object?)l.ArticleId ?? DBNull.Value);
                    Db.AddParam(ins, "@vid", (object?)l.VarianteId ?? DBNull.Value);
                    Db.AddParam(ins, "@cr", (object?)cotRateId ?? DBNull.Value);
                    Db.AddParam(ins, "@dl", l.Id);
                    ins.ExecuteNonQuery();
                }
            }

            // 5) total (dans la même tx)
            using (var sum = cn.CreateCommand())
            {
                sum.Transaction = tx;
                sum.CommandText = "SELECT COALESCE(SUM(Montant),0) FROM FactureLignes WHERE FactureId=@f;";
                Db.AddParam(sum, "@f", facId);
                var lignes = Convert.ToDecimal(sum.ExecuteScalar() ?? 0m, System.Globalization.CultureInfo.InvariantCulture);

                var total = lignes - d.RemiseGlobale; // d.RemiseGlobale est non-nullable (decimal)
                if (total < 0m) total = 0m;

                using var upd = cn.CreateCommand();
                upd.Transaction = tx;
                upd.CommandText = "UPDATE Factures SET Total=@t WHERE Id=@f;";
                Db.AddParam(upd, "@t", total);
                Db.AddParam(upd, "@f", facId);
                upd.ExecuteNonQuery();
            }

            tx.Commit();
            return facId;
        }


        // Marquer e-mail envoyé (Etat -> Envoye)
        public void MarkEmailSent(int factureId)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "UPDATE Factures SET SentAt=DATETIME('now'), Etat='Envoye' WHERE Id=@id;";
            Db.AddParam(cmd, "@id", factureId);
            cmd.ExecuteNonQuery();
        }

        [SupportedOSPlatform("windows")]
        // Générer PDF de la facture (retourne le chemin)
        public string RegeneratePdf(int factureId)
        {
            var f = GetById(factureId) ?? throw new InvalidOperationException("Facture introuvable.");
            var lignes = GetLines(factureId);

            // Dossier de sortie
            var dir = Path.Combine(Paths.DataDir, "Factures");
            Directory.CreateDirectory(dir);

            var numero = string.IsNullOrWhiteSpace(f.Numero) ? $"FACT-DRAFT-{f.Id}" : f.Numero;
            var pdf = Path.Combine(dir, $"{numero}.pdf");

            // --- Préparer les données attendues par RenderSimpleInvoice ---
            // Nom + adresse client (snapshot si présent, sinon info Clients)
            string clientName, clientAddr, clientPhone = "", clientEmail = "";

            // On essaye d'abord de lire le snapshot en table Factures (si tes colonnes existent)
            using (var cn = Db.Open())
            {
                using var cmd = cn.CreateCommand();
                cmd.CommandText = @"
SELECT ClientSociete, ClientNomPrenom, ClientAdresseL1, ClientCodePostal, ClientVille,
       ClientTelephone, ClientEmail,
       RemiseGlobale, BankAccountId
FROM Factures WHERE Id=@id;";
                Db.AddParam(cmd, "@id", factureId);

                string? societe = null, nomPrenom = null, adr1 = null, cp = null, ville = null;
                decimal remiseEuro = 0m; // on peut le garder si tu t'en sers, sinon inutile
                int? bankId = null;
                string? payText = null, payPlanJson = null, noteTop = null, noteBottom = null;

                using var rd = cmd.ExecuteReader();
                if (rd.Read())
                {
                    societe = rd["ClientSociete"] as string;
                    nomPrenom = rd["ClientNomPrenom"] as string;
                    adr1 = rd["ClientAdresseL1"] as string;
                    cp = rd["ClientCodePostal"] as string;
                    ville = rd["ClientVille"] as string;
                    clientPhone = rd["ClientTelephone"] as string ?? "";
                    clientEmail = rd["ClientEmail"] as string ?? "";
                    remiseEuro = Convert.ToDecimal(rd["RemiseGlobale"] ?? 0m, CultureInfo.InvariantCulture);
                    bankId = rd["BankAccountId"] as int?;
                }

                // Fallback client (si snapshot vide)
                if (string.IsNullOrWhiteSpace(societe) && string.IsNullOrWhiteSpace(nomPrenom) && f.ClientId is int cid)
                {
                    using var c2 = cn.CreateCommand();
                    c2.CommandText = "SELECT Societe, NomPrenom, AdresseL1, CodePostal, Ville, Email, Telephone FROM Clients WHERE Id=@id;";
                    Db.AddParam(c2, "@id", cid);
                    using var rc = c2.ExecuteReader();
                    if (rc.Read())
                    {
                        societe = rc["Societe"] as string;
                        nomPrenom = rc["NomPrenom"] as string;
                        adr1 = rc["AdresseL1"] as string;
                        cp = rc["CodePostal"] as string;
                        ville = rc["Ville"] as string;
                        if (string.IsNullOrWhiteSpace(clientEmail)) clientEmail = rc["Email"] as string ?? "";
                        if (string.IsNullOrWhiteSpace(clientPhone)) clientPhone = rc["Telephone"] as string ?? "";
                    }
                }

                clientName = !string.IsNullOrWhiteSpace(societe) ? societe! :
                             !string.IsNullOrWhiteSpace(nomPrenom) ? nomPrenom! : "Destinataire non renseigné";

                clientAddr = string.Join("\n", new[]
                {
            adr1 ?? "",
            $"{cp} {ville}".Trim()
        }.Where(s => !string.IsNullOrWhiteSpace(s)));

                // -- Modalités : on calcule payText depuis PaymentTermsId via SettingsCatalogService
                var cat = new SettingsCatalogService();
                var term = f.PaymentTermsId.HasValue ? cat.GetPaymentTermById(f.PaymentTermsId.Value) : null;
                string? paymentModeText = term?.Name;
                string? paymentTermsText = term?.Body;
                payText = FormatPaymentText(term);

                // Lire IBAN/BIC/h titulaire si BankAccountId est renseigné
                string? bankHolder = null, bankName = null, iban = null, bic = null;
                if (bankId is int bid)
                {
                    using var rb = cn.CreateCommand();
                    rb.CommandText = "SELECT Holder,BankName,Iban,Bic FROM BankAccounts WHERE Id=@id;";
                    Db.AddParam(rb, "@id", bid);
                    using var rdb = rb.ExecuteReader();
                    if (rdb.Read())
                    {
                        bankHolder = rdb["Holder"] as string;
                        bankName = rdb["BankName"] as string;
                        iban = rdb["Iban"] as string;
                        bic = rdb["Bic"] as string;
                    }
                }

                // Lignes -> tuple attendu (designation, qty, pu)
                var tuples = lignes.ConvertAll(l => (l.Designation ?? "", (double)l.Qty, (double)l.PU));

                // Génération (micro : mention 293B auto côté modèle)
                InvoicePdf.RenderSimpleInvoice(
                    outputPath: pdf,
                    numero: numero,
                    clientName: clientName,
                    clientAddr: clientAddr,
                    lines: tuples,
                    showMention293B: true,
                    discountEuro: f.RemiseGlobale,
                    bankHolder: bankHolder,
                    bankName: bankName,
                    iban: iban,
                    bic: bic,
                    paymentTermsText: paymentTermsText,
                    paymentPlanJson: payPlanJson,
                    noteTop: noteTop,
                    noteBottom: noteBottom,
                    devisDateText: f.Date.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("fr-FR")),
                    clientPhone: clientPhone,
                    clientEmail: clientEmail,
                    devisId: f.DevisSourceId,
                    docTitle: "Facture",
                    validityText: null,
                    paymentModeText: paymentModeText
                );
            }

            return pdf;
        }

        [SupportedOSPlatform("windows")]
        // Envoi e-mail (PDF joint) – modèle simple
        public async Task SendAndLogAsync(int factureId)
        {
            var f = GetById(factureId) ?? throw new InvalidOperationException("Facture introuvable.");
            if (string.IsNullOrWhiteSpace(f.Numero))
                throw new InvalidOperationException("La facture n’est pas numérotée.");

            var pdfPath = RegeneratePdf(factureId);

            var catalogs = new SettingsCatalogService();
            var accounts = catalogs.GetEmailAccounts();
            var account = accounts.FirstOrDefault(a => a.IsDefault) ?? accounts.FirstOrDefault()
                          ?? throw new InvalidOperationException("Aucun compte e-mail configuré.");

            var templates = catalogs.GetEmailTemplates();
            var tpl = templates.FirstOrDefault(t => t.Name.Contains("Facture", StringComparison.OrdinalIgnoreCase))
                     ?? templates.FirstOrDefault()
                     ?? throw new InvalidOperationException("Aucun modèle e-mail disponible.");

            var emailSvc = new EmailService();

            var map = new Dictionary<string, string?>
            {
                ["INVOICE_NUMBER"] = f.Numero,
                ["INVOICE_TOTAL"] = f.Total.ToString("0.00"),
                ["CURRENCY"] = "€"
            };

            var subject = emailSvc.RenderTemplate(tpl.Subject, map);
            var body = emailSvc.RenderTemplate(tpl.Body, map);

            var to = catalogs.GetCompanyProfile().Email; // fallback par défaut
            if (f.ClientId is int cid)
            {
                using var cn = Db.Open();
                using var cmd = cn.CreateCommand();
                cmd.CommandText = "SELECT Email FROM Clients WHERE Id=@id;";
                Db.AddParam(cmd, "@id", cid);
                var mail = cmd.ExecuteScalar() as string;
                if (!string.IsNullOrWhiteSpace(mail)) to = mail!;
            }

            await emailSvc.SendAndLogAsync(
                account: account,
                to: to,
                subject: subject,
                body: body,
                isHtml: tpl.IsHtml,
                attachmentPaths: new[] { pdfPath },
                context: $"FACTURE:{f.Id}"
            );

            MarkEmailSent(factureId);
        }

        public void SetGlobalDiscount(int factureId, decimal remise)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "UPDATE Factures SET RemiseGlobale=@r WHERE Id=@id;";
            Db.AddParam(cmd, "@r", remise);
            Db.AddParam(cmd, "@id", factureId);
            cmd.ExecuteNonQuery();
        }

        public void UpdateClientFields(int factureId,
            string? societe, string? nomPrenom, string? adrL1,
            string? cp, string? ville, string? email, string? tel)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
UPDATE Factures SET
  ClientSociete=@cs, ClientNomPrenom=@cnp, ClientAdresseL1=@cadr,
  ClientCodePostal=@cp, ClientVille=@cv, ClientEmail=@ce, ClientTelephone=@ctel
WHERE Id=@id;";
            Db.AddParam(cmd, "@cs", societe);
            Db.AddParam(cmd, "@cnp", nomPrenom);
            Db.AddParam(cmd, "@cadr", adrL1);
            Db.AddParam(cmd, "@cp", cp);
            Db.AddParam(cmd, "@cv", ville);
            Db.AddParam(cmd, "@ce", email);
            Db.AddParam(cmd, "@ctel", tel);
            Db.AddParam(cmd, "@id", factureId);
            cmd.ExecuteNonQuery();
        }

        public void SetPaymentTerms(int factureId, int? paymentTermsId)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "UPDATE Factures SET PaymentTermsId=@p WHERE Id=@id;";
            Db.AddParam(cmd, "@p", (object?)paymentTermsId ?? DBNull.Value);
            Db.AddParam(cmd, "@id", factureId);
            cmd.ExecuteNonQuery();
        }

        public void SetBankAccount(int factureId, int? bankAccountId)
        {
            using var cn = Db.Open();

            if (bankAccountId == null)
            {
                // Aucune banque sélectionnée : on efface aussi le snapshot
                using var clr = cn.CreateCommand();
                clr.CommandText = @"
UPDATE Factures
   SET BankAccountId = NULL,
       BankHolder    = NULL,
        BankName     = NULL,
        Iban         = NULL,
        Bic          = NULL
 WHERE Id = @id;";
                Db.AddParam(clr, "@id", factureId);
                clr.ExecuteNonQuery();
                return;
            }

            // Charger la banque et snapshotter
            string? holder = null, bankName = null, iban = null, bic = null;
            using (var get = cn.CreateCommand())
            {
                get.CommandText = @"SELECT Holder, BankName, Iban, Bic
                              FROM BankAccounts
                             WHERE Id=@bid;";
                Db.AddParam(get, "@bid", bankAccountId.Value);
                using var rd = get.ExecuteReader();
                if (rd.Read())
                {
                    holder = rd["Holder"] as string;
                    bankName = rd["BankName"] as string;
                    iban = rd["Iban"] as string;
                    bic = rd["Bic"] as string;
                }
                else
                {
                    // banque introuvable -> on déselectionne
                    bankAccountId = null;
                }
            }

            using (var up = cn.CreateCommand())
            {
                up.CommandText = @"
UPDATE Factures
   SET BankAccountId = @bid,
       BankHolder    = @bh,
       BankName      = @bn,
       Iban          = @iban,
       Bic           = @bic
 WHERE Id = @id;";
                Db.AddParam(up, "@bid", (object?)bankAccountId ?? DBNull.Value);
                Db.AddParam(up, "@bh", (object?)holder ?? DBNull.Value);
                Db.AddParam(up, "@bn", (object?)bankName ?? DBNull.Value);
                Db.AddParam(up, "@iban", (object?)iban ?? DBNull.Value);
                Db.AddParam(up, "@bic", (object?)bic ?? DBNull.Value);
                Db.AddParam(up, "@id", factureId);
                up.ExecuteNonQuery();
            }
        }

        public void SoftDelete(int factureId)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "UPDATE Factures SET DeletedAt=@ts WHERE Id=@id;";
            Db.AddParam(cmd, "@ts", DateTime.Now.ToString("s"));
            Db.AddParam(cmd, "@id", factureId);
            cmd.ExecuteNonQuery();
        }

        public void SetClientSnapshot(int factureId, int? clientId, Models.Client? client = null)
        {
            using var cn = Db.Open();

            // si on nous donne un clientId mais pas l'objet, on le charge depuis la table Clients
            if (client == null && clientId.HasValue)
            {
                using var get = cn.CreateCommand();
                get.CommandText = "SELECT Id,Nom,Prenom,Societe,Adresse,CodePostal,Ville,Email,Telephone FROM Clients WHERE Id=@id;";
                Db.AddParam(get, "@id", clientId.Value);
                using var rd = get.ExecuteReader();
                if (rd.Read())
                {
                    client = new Models.Client
                    {
                        Id = Convert.ToInt32(rd["Id"]),
                        Nom = (rd["Nom"]?.ToString() ?? "").Trim(),       // <= plus de CS8601
                        Prenom = (rd["Prenom"]?.ToString() ?? "").Trim(),    // <= plus de CS8601
                        Societe = rd["Societe"]?.ToString(),
                        Adresse = rd["Adresse"]?.ToString(),
                        CodePostal = rd["CodePostal"]?.ToString(),
                        Ville = rd["Ville"]?.ToString(),
                        Email = rd["Email"]?.ToString(),
                        Telephone = rd["Telephone"]?.ToString()
                    };
                }
            }

            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
UPDATE Factures SET
  ClientId=@cid,
  ClientSociete=@cs,
  ClientNomPrenom=@cnp,
  ClientAdresseL1=@cadr,
  ClientCodePostal=@cp,
  ClientVille=@cv,
  ClientEmail=@ce,
  ClientTelephone=@ctel
WHERE Id=@id;";
            Db.AddParam(cmd, "@cid", (object?)clientId ?? DBNull.Value);
            Db.AddParam(cmd, "@cs", client?.Societe);
            Db.AddParam(cmd, "@cnp", $"{(client?.Prenom ?? "").Trim()} {(client?.Nom ?? "").Trim()}".Trim());
            Db.AddParam(cmd, "@cadr", client?.Adresse);
            Db.AddParam(cmd, "@cp", client?.CodePostal);
            Db.AddParam(cmd, "@cv", client?.Ville);
            Db.AddParam(cmd, "@ce", client?.Email);
            Db.AddParam(cmd, "@ctel", client?.Telephone);
            Db.AddParam(cmd, "@id", factureId);
            cmd.ExecuteNonQuery();
        }

        // === PDF (facture) ===
        [SupportedOSPlatform("windows")]
        public string BuildInvoicePdf(int factureId)
        {
            // 1) Charger entête + lignes
            var f = GetById(factureId) ?? throw new InvalidOperationException("Facture introuvable.");
            var fLines = GetLines(factureId);

            // 2) Lignes pour InvoicePdf: (designation, double qty, double pu)
            var lines = new List<(string designation, double qty, double pu)>();
            foreach (var l in fLines)
            {
                lines.Add(((l.Designation ?? "").Trim(),
                           (double)l.Qty,
                           (double)l.PU));
            }

            // 3) Destinataire (snapshots facture)
            var blocNom = string.IsNullOrWhiteSpace(f.ClientSociete)
                            ? (f.ClientNomPrenom ?? "")
                            : f.ClientSociete!;
            var adrParts = new List<string?>();
            if (!string.IsNullOrWhiteSpace(f.ClientAdresseL1)) adrParts.Add(f.ClientAdresseL1);
            var cpVille = $"{f.ClientCodePostal} {f.ClientVille}".Trim();
            if (!string.IsNullOrWhiteSpace(cpVille)) adrParts.Add(cpVille);

            var clientName = (blocNom ?? "").Trim();
            var clientAddr = string.Join("\n", adrParts.Where(s => !string.IsNullOrWhiteSpace(s)));
            var clientTel = f.ClientTelephone ?? "";
            var clientEmail = f.ClientEmail ?? "";

            // 4) Banque + Modalités
            var bankHolder = f.BankHolder ?? "";
            var bankName = f.BankName ?? "";
            var iban = f.Iban ?? "";
            var bic = f.Bic ?? "";

            var cat = new SettingsCatalogService();
            PaymentTerm? term = f.PaymentTermsId.HasValue ? cat.GetPaymentTermById(f.PaymentTermsId.Value) : null;

            string payText = term?.Name ?? "";
            string payPlanJson = ""; // non utilisé dans tes modèles actuels

            // 5) Numéro + chemin PDF
            var numero = string.IsNullOrWhiteSpace(f.Numero) ? "(Brouillon)" : f.Numero!;
            var outDir = System.IO.Path.Combine(Paths.DataDir, "Factures");
            System.IO.Directory.CreateDirectory(outDir);
            var pdf = System.IO.Path.Combine(outDir, $"{numero}.pdf");

            // 6) Render
            //    - signature attend : discountEuro (decimal)
            //    - pas de doublon sur clientEmail
            InvoicePdf.RenderSimpleInvoice(
                outputPath: pdf,
                numero: numero,
                clientName: clientName,
                clientAddr: clientAddr,
                lines: lines,
                showMention293B: true,
                discountEuro: f.RemiseGlobale,
                bankHolder: bankHolder,
                bankName: bankName,
                iban: iban,
                bic: bic,
                paymentTermsText: payText,
                paymentPlanJson: payPlanJson,
                noteTop: null,
                noteBottom: null,
                devisDateText: null,
                clientPhone: clientTel,
                clientEmail: clientEmail,
                devisId: f.DevisSourceId
            );

            return pdf;
        }

        private static string FormatPaymentText(PaymentTerm? t)
        {
            if (t == null) return "";

            // Si tu stockes un code machine dans SimpleDue, on le rend humain
            string human(string? code)
            {
                return (code ?? "").Trim().ToUpperInvariant() switch
                {
                    "AT_ORDER" => "à la commande",
                    "AT_DELIVERY" => "à la livraison",
                    "NET_30" => "à 30 jours",
                    "NET_45" => "à 45 jours",
                    "NET_60" => "à 60 jours",
                    _ => code ?? ""
                };
            }

            // SIMPLE => on compose proprement
            if (string.Equals(t.Mode, "SIMPLE", StringComparison.OrdinalIgnoreCase))
            {
                var right = human(t.SimpleDue);
                if (string.IsNullOrWhiteSpace(right)) return t.Name ?? "";
                // évite les doublons du style "Règlement à la commande — AT_ORDER"
                if (!string.IsNullOrWhiteSpace(t.Name) &&
                    right.Equals(t.Name, StringComparison.OrdinalIgnoreCase))
                    return right;

                return $"{t.Name ?? "Règlement"} — {right}";
            }

            // autres modes : garde le nom
            return t.Name ?? "";
        }

        public void MarkSent(int factureId)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "UPDATE Factures SET Etat='Envoye', SentAt=DATETIME('now') WHERE Id=@id;";
            Db.AddParam(cmd, "@id", factureId);
            cmd.ExecuteNonQuery();
        }


    }
}
