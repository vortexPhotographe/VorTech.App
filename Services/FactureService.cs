using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using VorTech.App;
using VorTech.App.Models;



namespace VorTech.App.Services
{
    public class FactureService
    {
        private readonly ArticleService _articles = new ArticleService();

        // --- CRUD entête ---
        public int CreateDraft(int? clientId, int? fromDevisId = null)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
INSERT INTO Factures(Numero, ClientId, Date, Etat, RemiseGlobale, Total, PaymentTermsId, D, DevisSourceId)
VALUES(NULL, @c, DATE('now'), 'Brouillon', 0, 0, NULL, 0, @ds);
SELECT last_insert_rowid();";
            Db.AddParam(cmd, "@c", (object?)clientId ?? DBNull.Value);
            Db.AddParam(cmd, "@ds", (object?)fromDevisId ?? DBNull.Value);
            return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        }

        public Facture? GetById(int id)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT * FROM Factures WHERE Id=@id;";
            Db.AddParam(cmd, "@id", id);
            using var rd = cmd.ExecuteReader();
            if (!rd.Read()) return null;

            return new Facture
            {
                Id = id,
                Numero = rd["Numero"]?.ToString(),
                ClientId = rd["ClientId"] as int?,
                Date = DateOnly.Parse(rd["Date"]?.ToString() ?? DateTime.Today.ToString("yyyy-MM-dd")),
                Etat = rd["Etat"]?.ToString() ?? "Brouillon",
                RemiseGlobale = Convert.ToDecimal(rd["RemiseGlobale"] ?? 0m, CultureInfo.InvariantCulture),
                Total = Convert.ToDecimal(rd["Total"] ?? 0m, CultureInfo.InvariantCulture),
                PaymentTermsId = rd["PaymentTermsId"] as int?,
                SentAt = TryParseDateTime(rd["SentAt"]?.ToString()),
                DeletedAt = TryParseDateTime(rd["DeletedAt"]?.ToString()),
                D = Convert.ToInt32(rd["D"] ?? 0) != 0,
                DevisSourceId = rd["DevisSourceId"] as int?
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

        // --- Numérotation (émission) ---
        public string EmitNumero(int factureId, INumberingService num)
        {
            using var cn = Db.Open();
            using var tx = cn.BeginTransaction();

            string? current = null;
            DateOnly docDate = DateOnly.FromDateTime(DateTime.Today);

            using (var get = cn.CreateCommand())
            {
                get.Transaction = tx;
                get.CommandText = "SELECT Numero, Date FROM Factures WHERE Id=@id;";
                Db.AddParam(get, "@id", factureId);
                using var rd = get.ExecuteReader();
                if (!rd.Read())
                    throw new InvalidOperationException("Facture introuvable.");

                current = rd.IsDBNull(0) ? null : rd.GetString(0);
                if (!rd.IsDBNull(1))
                {
                    var s = rd.GetValue(1)?.ToString();
                    if (!string.IsNullOrWhiteSpace(s) && DateOnly.TryParse(s, out var d))
                        docDate = d;
                }
            }

            if (!string.IsNullOrWhiteSpace(current))
            {
                tx.Commit();
                return current!;
            }

            var newNumero = num.Next("FACT", docDate);   // format seedé FACT-{yyyy}-{MM}-{####} (scope MONTHLY)
            using (var upd = cn.CreateCommand())
            {
                upd.Transaction = tx;
                upd.CommandText = "UPDATE Factures SET Numero=@n, Etat='Cree' WHERE Id=@id;";
                Db.AddParam(upd, "@n", newNumero);
                Db.AddParam(upd, "@id", factureId);
                upd.ExecuteNonQuery();
            }

            // Numérotation faite : on passe à la décrémentation de stock dans la même TX
            using (var sel = cn.CreateCommand())
            {
                sel.Transaction = tx;
                sel.CommandText = @"SELECT ArticleId, VarianteId, Qty
                        FROM FactureLignes WHERE FactureId=@f;";
                Db.AddParam(sel, "@f", factureId);

                using var rd = sel.ExecuteReader();
                while (rd.Read())
                {
                    int? articleId = rd.IsDBNull(0) ? (int?)null : rd.GetInt32(0);
                    int? varianteId = rd.IsDBNull(1) ? (int?)null : rd.GetInt32(1);
                    decimal qty = Convert.ToDecimal(rd.GetValue(2) ?? 0);

                    if (qty <= 0) continue;

                    // priorité variante si renseignée
                    if (varianteId.HasValue)
                    {
                        using var upd = cn.CreateCommand();
                        upd.Transaction = tx;
                        upd.CommandText = @"
UPDATE Stock
   SET Qte = COALESCE(Qte,0) - @q
 WHERE (VarianteId = @vid);";
                        Db.AddParam(upd, "@q", qty);
                        Db.AddParam(upd, "@vid", varianteId.Value);
                        upd.ExecuteNonQuery();
                    }
                    else if (articleId.HasValue)
                    {
                        using var upd = cn.CreateCommand();
                        upd.Transaction = tx;
                        upd.CommandText = @"
UPDATE Stock
   SET Qte = COALESCE(Qte,0) - @q
 WHERE (ArticleId = @aid) AND (VarianteId IS NULL OR VarianteId=0);";
                        Db.AddParam(upd, "@q", qty);
                        Db.AddParam(upd, "@aid", articleId.Value);
                        upd.ExecuteNonQuery();
                    }
                }
            }

            tx.Commit();
            return newNumero;
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
                var lignes = Convert.ToDecimal(sum.ExecuteScalar() ?? 0m, CultureInfo.InvariantCulture);

                var total = lignes - d.RemiseGlobale;   // <-- plus de ??
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

        public void SetPaymentTerms(int factureId, int? paymentTermsId)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "UPDATE Factures SET PaymentTermsId=@pt WHERE Id=@id;";
            Db.AddParam(cmd, "@pt", (object?)paymentTermsId ?? DBNull.Value);
            Db.AddParam(cmd, "@id", factureId);
            cmd.ExecuteNonQuery();
        }

        public void SetBankAccount(int factureId, int? bankId)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            // Ajoute la colonne si pas encore ajoutée chez toi
            cmd.CommandText = "ALTER TABLE Factures ADD COLUMN BankAccountId INTEGER;";
            try { cmd.ExecuteNonQuery(); } catch { /* déjà là */ }

            using var up = cn.CreateCommand();
            up.CommandText = "UPDATE Factures SET BankAccountId=@b WHERE Id=@id;";
            Db.AddParam(up, "@b", (object?)bankId ?? DBNull.Value);
            Db.AddParam(up, "@id", factureId);
            up.ExecuteNonQuery();
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
       RemiseGlobale, PaymentTermsText, PaymentPlanJson, NoteHaut, NoteBas, BankAccountId
FROM Factures WHERE Id=@id;";
                Db.AddParam(cmd, "@id", factureId);

                string? societe = null, nomPrenom = null, adr1 = null, cp = null, ville = null;
                decimal remiseEuro = 0m;
                string? payText = null, payPlanJson = null, noteTop = null, noteBottom = null;
                int? bankId = null;

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
                    payText = rd["PaymentTermsText"] as string;
                    payPlanJson = rd["PaymentPlanJson"] as string;
                    noteTop = rd["NoteHaut"] as string;
                    noteBottom = rd["NoteBas"] as string;
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
                    discountEuro: f.RemiseGlobale,         // ou remiseEuro si tu préfères le snapshot
                    bankHolder: bankHolder,
                    bankName: bankName,
                    iban: iban,
                    bic: bic,
                    paymentTermsText: payText,
                    paymentPlanJson: payPlanJson,
                    noteTop: noteTop,
                    noteBottom: noteBottom,
                    clientPhone: clientPhone,
                    clientEmail: clientEmail,
                    devisId: f.DevisSourceId               // peut être null, c'est ok
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

        public void SetClientSnapshot(int factureId, int? clientId)
        {
            using var cn = Db.Open();

            if (clientId == null)
            {
                using var clr = cn.CreateCommand();
                clr.CommandText = @"
UPDATE Factures SET
  ClientId = NULL,
  ClientSociete = NULL,
  ClientNomPrenom = NULL,
  ClientAdresseL1 = NULL,
  ClientCodePostal = NULL,
  ClientVille = NULL,
  ClientEmail = NULL,
  ClientTelephone = NULL
WHERE Id=@id;";
                Db.AddParam(clr, "@id", factureId);
                clr.ExecuteNonQuery();
                return;
            }

            string? societe = null, nomPrenom = null, adr1 = null, cp = null, ville = null, email = null, tel = null;

            using (var get = cn.CreateCommand())
            {
                get.CommandText = @"
SELECT 
  Societe,
  TRIM(COALESCE(Nom,'') || CASE WHEN TRIM(COALESCE(Nom,''))<>'' AND TRIM(COALESCE(Prenom,''))<>'' THEN ' ' ELSE '' END || COALESCE(Prenom,'')) AS NomPrenom,
  Adresse AS AdresseL1,
  CodePostal,
  Ville,
  Email,
  Telephone
FROM Clients WHERE Id=@cid;";
                Db.AddParam(get, "@cid", clientId.Value);
                using var rd = get.ExecuteReader();
                if (rd.Read())
                {
                    societe = rd["Societe"] as string;
                    nomPrenom = rd["NomPrenom"] as string;
                    adr1 = rd["AdresseL1"] as string;
                    cp = rd["CodePostal"] as string;
                    ville = rd["Ville"] as string;
                    email = rd["Email"] as string;
                    tel = rd["Telephone"] as string;
                }
                else
                {
                    throw new InvalidOperationException("Client introuvable.");
                }
            }

            using (var up = cn.CreateCommand())
            {
                up.CommandText = @"
UPDATE Factures SET
  ClientId=@cid,
  ClientSociete=@cs, ClientNomPrenom=@cnp, ClientAdresseL1=@cadr,
  ClientCodePostal=@cp, ClientVille=@cv, ClientEmail=@ce, ClientTelephone=@ctel
WHERE Id=@id;";
                Db.AddParam(up, "@cid", clientId.Value);
                Db.AddParam(up, "@cs", (object?)societe ?? DBNull.Value);
                Db.AddParam(up, "@cnp", (object?)nomPrenom ?? DBNull.Value);
                Db.AddParam(up, "@cadr", (object?)adr1 ?? DBNull.Value);
                Db.AddParam(up, "@cp", (object?)cp ?? DBNull.Value);
                Db.AddParam(up, "@cv", (object?)ville ?? DBNull.Value);
                Db.AddParam(up, "@ce", (object?)email ?? DBNull.Value);
                Db.AddParam(up, "@ctel", (object?)tel ?? DBNull.Value);
                Db.AddParam(up, "@id", factureId);
                up.ExecuteNonQuery();
            }
        }
    }
}
