using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Runtime.Versioning;
using System.Text.Json;
using VorTech.App.Models;
using static QRCoder.PayloadGenerator.SwissQrCode;

namespace VorTech.App.Services
{
    public class DevisService : IDevisService
    {
        public DevisService() => EnsureSchema();

        private void EnsureSchema()
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS Devis (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  Numero TEXT UNIQUE,
  Date TEXT NOT NULL,
  Etat TEXT NOT NULL DEFAULT 'Brouillon',
  ClientId INTEGER NULL,
  ClientNom TEXT, ClientEmail TEXT, ClientTelephone TEXT,
  ClientAdresseL1 TEXT, ClientCodePostal TEXT, ClientVille TEXT,
  NoteHaut TEXT, NoteBas TEXT,
  RemiseGlobale REAL NOT NULL DEFAULT 0,
  Total REAL NOT NULL DEFAULT 0,
  DeletedAt TEXT NULL,
  BankAccountId INTEGER NULL
);
CREATE INDEX IF NOT EXISTS IX_Devis_Date ON Devis(Date);
CREATE INDEX IF NOT EXISTS IX_Devis_Client ON Devis(ClientId);

CREATE TABLE IF NOT EXISTS DevisLignes (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  DevisId INTEGER NOT NULL,
  Designation TEXT NOT NULL,
  Qty REAL NOT NULL DEFAULT 1,
  PU REAL NOT NULL DEFAULT 0,
  Montant REAL NOT NULL DEFAULT 0,
  ArticleId INTEGER NULL,
  VarianteId INTEGER NULL,
  ImagePath TEXT NULL,
  FOREIGN KEY(DevisId) REFERENCES Devis(Id)
);
CREATE INDEX IF NOT EXISTS IX_DevisLignes_Devis ON DevisLignes(DevisId);

CREATE TABLE IF NOT EXISTS DevisAnnexes (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  DevisId INTEGER NOT NULL,
  Type TEXT NOT NULL,
  Chemin TEXT,
  Ordre INTEGER NOT NULL DEFAULT 0,
  ConfigJson TEXT NULL,
  FOREIGN KEY(DevisId) REFERENCES Devis(Id)
);
CREATE INDEX IF NOT EXISTS IX_DevisAnnexes_Devis ON DevisAnnexes(DevisId);

CREATE TABLE IF NOT EXISTS DevisOptions (
  Id         INTEGER PRIMARY KEY AUTOINCREMENT,
  DevisId    INTEGER NOT NULL,
  ArticleId  INTEGER NULL,
  VarianteId INTEGER NULL,
  Libelle    TEXT    NOT NULL,
  Prix       REAL    NOT NULL DEFAULT 0,
  FOREIGN KEY(DevisId) REFERENCES Devis(Id)
);
CREATE INDEX IF NOT EXISTS IX_DevisOptions_Devis ON DevisOptions(DevisId);";


            cmd.ExecuteNonQuery();
            try
            {
                using var alter = cn.CreateCommand();
                alter.CommandText = "ALTER TABLE Devis ADD COLUMN BankAccountId INTEGER NULL;";
                alter.ExecuteNonQuery();
            }
            catch { /* colonne déjà présente */ }

            try
            {
                using var add1 = cn.CreateCommand();
                add1.CommandText = "ALTER TABLE Devis ADD COLUMN ClientSociete TEXT NULL;";
                add1.ExecuteNonQuery();
            }
            catch { /* colonne déjà là */ }

            try
            {
                using var add2 = cn.CreateCommand();
                add2.CommandText = "ALTER TABLE Devis ADD COLUMN ClientNomPrenom TEXT NULL;";
                add2.ExecuteNonQuery();
            }
            catch { /* colonne déjà là */ }

            try
            {
                using var add3 = cn.CreateCommand();
                add3.CommandText = "ALTER TABLE Devis ADD COLUMN PaymentTermsId INTEGER NULL;";
                add3.ExecuteNonQuery();
            }
            catch { /* déjà là */ }

            try
            {
                using var add4 = cn.CreateCommand();
                add4.CommandText = "ALTER TABLE Devis ADD COLUMN PaymentTermsText TEXT NULL;";
                add4.ExecuteNonQuery();
            }
            catch { /* déjà là */ }

            try
            {
                using var add5 = cn.CreateCommand();
                add5.CommandText = "ALTER TABLE Devis ADD COLUMN PaymentPlanJson TEXT NULL;";
                add5.ExecuteNonQuery();
            }
            catch { /* déjà là */ }

            try
            {
                using var addSent = cn.CreateCommand();
                addSent.CommandText = "ALTER TABLE Devis ADD COLUMN SentAt TEXT NULL;";
                addSent.ExecuteNonQuery();
            }
            catch { /* déjà là */ }

        }

        public int CreateDraft(int? clientId, Client? s = null)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
INSERT INTO Devis(Date, Etat, ClientId, ClientNom, ClientEmail, ClientTelephone, ClientAdresseL1, ClientCodePostal, ClientVille)
VALUES(@d, 'Brouillon', @cid, @nom, @mail, @tel, @a1, @cp, @ville);
SELECT last_insert_rowid();";
            var iso = DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            Db.AddParam(cmd, "@d", iso);
            Db.AddParam(cmd, "@cid", (object?)clientId ?? DBNull.Value);
            Db.AddParam(cmd, "@nom", s?.Nom ?? "");
            Db.AddParam(cmd, "@mail", s?.Email ?? "");
            Db.AddParam(cmd, "@tel", s?.Telephone ?? "");
            Db.AddParam(cmd, "@a1", s?.Adresse ?? "");
            Db.AddParam(cmd, "@cp", s?.CodePostal ?? "");
            Db.AddParam(cmd, "@ville", s?.Ville ?? "");
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public void SetNotes(int id, string? haut, string? bas)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "UPDATE Devis SET NoteHaut=@h, NoteBas=@b WHERE Id=@id;";
            Db.AddParam(cmd, "@h", (object?)haut ?? DBNull.Value);
            Db.AddParam(cmd, "@b", (object?)bas ?? DBNull.Value);
            Db.AddParam(cmd, "@id", id);
            cmd.ExecuteNonQuery();
        }

        public void SetGlobalDiscount(int id, decimal remise)
        {
            if (remise < 0m) remise = 0m;
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "UPDATE Devis SET RemiseGlobale=@r WHERE Id=@id;";
            Db.AddParam(cmd, "@r", remise);
            Db.AddParam(cmd, "@id", id);
            cmd.ExecuteNonQuery();
            RecalcTotals(id);
        }

        public int AddLine(int devisId, string designation, decimal qty, decimal pu, int? articleId = null, int? varianteId = null, string? imagePath = null)
        {
            if (qty < 0m || pu < 0m) throw new InvalidOperationException("Valeurs négatives interdites");
            var montant = qty * pu;
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
INSERT INTO DevisLignes(DevisId, Designation, Qty, PU, Montant, ArticleId, VarianteId, ImagePath)
VALUES(@d,@lib,@q,@pu,@m,@aid,@vid,@img);
SELECT last_insert_rowid();";
            Db.AddParam(cmd, "@d", devisId);
            Db.AddParam(cmd, "@lib", designation ?? "");
            Db.AddParam(cmd, "@q", qty);
            Db.AddParam(cmd, "@pu", pu);
            Db.AddParam(cmd, "@m", montant < 0m ? 0m : montant);
            Db.AddParam(cmd, "@aid", (object?)articleId ?? DBNull.Value);
            Db.AddParam(cmd, "@vid", (object?)varianteId ?? DBNull.Value);
            Db.AddParam(cmd, "@img", (object?)imagePath ?? DBNull.Value);
            var id = Convert.ToInt32(cmd.ExecuteScalar());
            RecalcTotals(devisId);
            return id;
        }

        public void UpdateLine(int ligneId, string designation, decimal qty, decimal pu)
        {
            if (qty < 0m || pu < 0m) throw new InvalidOperationException("Valeurs négatives interdites");
            using var cn = Db.Open();

            int devisId;
            using (var get = cn.CreateCommand())
            {
                get.CommandText = "SELECT DevisId FROM DevisLignes WHERE Id=@id;";
                Db.AddParam(get, "@id", ligneId);
                devisId = Convert.ToInt32(get.ExecuteScalar() ?? 0);
                if (devisId == 0) return;
            }

            var m = qty * pu;
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = "UPDATE DevisLignes SET Designation=@lib, Qty=@q, PU=@pu, Montant=@m WHERE Id=@id;";
                Db.AddParam(cmd, "@lib", designation ?? "");
                Db.AddParam(cmd, "@q", qty);
                Db.AddParam(cmd, "@pu", pu);
                Db.AddParam(cmd, "@m", m < 0m ? 0m : m);
                Db.AddParam(cmd, "@id", ligneId);
                cmd.ExecuteNonQuery();
            }

            RecalcTotals(devisId);
        }

        public void DeleteLine(int ligneId)
        {
            using var cn = Db.Open();

            int devisId;
            using (var get = cn.CreateCommand())
            {
                get.CommandText = "SELECT DevisId FROM DevisLignes WHERE Id=@id;";
                Db.AddParam(get, "@id", ligneId);
                devisId = Convert.ToInt32(get.ExecuteScalar() ?? 0);
                if (devisId == 0) return;
            }

            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM DevisLignes WHERE Id=@id;";
                Db.AddParam(cmd, "@id", ligneId);
                cmd.ExecuteNonQuery();
            }

            RecalcTotals(devisId);
        }

        public void RecalcTotals(int devisId)
        {
            using var cn = Db.Open();

            decimal lignes = 0m;
            using (var sum = cn.CreateCommand())
            {
                sum.CommandText = "SELECT COALESCE(SUM(Montant),0) FROM DevisLignes WHERE DevisId=@d;";
                Db.AddParam(sum, "@d", devisId);
                lignes = Convert.ToDecimal(sum.ExecuteScalar() ?? 0m, CultureInfo.InvariantCulture);
            }

            decimal remise;
            using (var get = cn.CreateCommand())
            {
                get.CommandText = "SELECT RemiseGlobale FROM Devis WHERE Id=@d;";
                Db.AddParam(get, "@d", devisId);
                remise = Convert.ToDecimal(get.ExecuteScalar() ?? 0m, CultureInfo.InvariantCulture);
            }

            var total = lignes - remise;
            if (total < 0m) total = 0m;

            using (var upd = cn.CreateCommand())
            {
                upd.CommandText = "UPDATE Devis SET Total=@t WHERE Id=@d;";
                Db.AddParam(upd, "@t", total);
                Db.AddParam(upd, "@d", devisId);
                upd.ExecuteNonQuery();
            }
        }

        public void AttachAnnexPdf(int devisId, string relPath, int ordre)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "INSERT INTO DevisAnnexes(DevisId,Type,Chemin,Ordre) VALUES(@d,'PDF',@p,@o);";
            Db.AddParam(cmd, "@d", devisId);
            Db.AddParam(cmd, "@p", relPath ?? "");
            Db.AddParam(cmd, "@o", ordre);
            cmd.ExecuteNonQuery();
        }

        public void AttachAnnexPlanche(int devisId, int ordre, object? config = null)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "INSERT INTO DevisAnnexes(DevisId,Type,Chemin,Ordre,ConfigJson) VALUES(@d,'PLANCHE',NULL,@o,@cfg);";
            Db.AddParam(cmd, "@d", devisId);
            Db.AddParam(cmd, "@o", ordre);
            Db.AddParam(cmd, "@cfg", config == null ? DBNull.Value : JsonSerializer.Serialize(config));
            cmd.ExecuteNonQuery();
        }

        public void ReorderAnnexe(int annexeId, int newOrdre)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "UPDATE DevisAnnexes SET Ordre=@o WHERE Id=@id;";
            Db.AddParam(cmd, "@o", newOrdre);
            Db.AddParam(cmd, "@id", annexeId);
            cmd.ExecuteNonQuery();
        }

        public void SetClientSnapshot(int devisId, int? clientId, Client? c)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
UPDATE Devis SET
  ClientId=@cid,
  ClientSociete=@soc,
  ClientNomPrenom=@np,
  ClientAdresseL1=@adr,
  ClientCodePostal=@cp,
  ClientVille=@ville,
  ClientEmail=@mail,
  ClientTelephone=@tel
WHERE Id=@id;";
            Db.AddParam(cmd, "@id", devisId);
            Db.AddParam(cmd, "@cid", (object?)clientId ?? DBNull.Value);
            var np = ((c?.Prenom ?? "").Trim() + " " + (c?.Nom ?? "").Trim()).Trim();
            Db.AddParam(cmd, "@soc", (object?)c?.Societe ?? DBNull.Value);
            Db.AddParam(cmd, "@np", string.IsNullOrWhiteSpace(np) ? DBNull.Value : np);
            Db.AddParam(cmd, "@adr", (object?)c?.Adresse ?? DBNull.Value);
            Db.AddParam(cmd, "@cp", (object?)c?.CodePostal ?? DBNull.Value);
            Db.AddParam(cmd, "@ville", (object?)c?.Ville ?? DBNull.Value);
            Db.AddParam(cmd, "@mail", (object?)c?.Email ?? DBNull.Value);
            Db.AddParam(cmd, "@tel", (object?)c?.Telephone ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        public void UpdateClientFields(int devisId, string? societe, string? nomPrenom,
                               string? adresse, string? cp, string? ville,
                               string? email, string? telephone)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
UPDATE Devis SET
  ClientSociete=@soc,
  ClientNomPrenom=@np,
  ClientAdresseL1=@adr,
  ClientCodePostal=@cp,
  ClientVille=@ville,
  ClientEmail=@mail,
  ClientTelephone=@tel
WHERE Id=@id;";
            Db.AddParam(cmd, "@id", devisId);
            Db.AddParam(cmd, "@soc", (object?)societe ?? DBNull.Value);
            Db.AddParam(cmd, "@np", (object?)nomPrenom ?? DBNull.Value);
            Db.AddParam(cmd, "@adr", (object?)adresse ?? DBNull.Value);
            Db.AddParam(cmd, "@cp", (object?)cp ?? DBNull.Value);
            Db.AddParam(cmd, "@ville", (object?)ville ?? DBNull.Value);
            Db.AddParam(cmd, "@mail", (object?)email ?? DBNull.Value);
            Db.AddParam(cmd, "@tel", (object?)telephone ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        // Mode de reglement 
        public void SetPaymentTerms(int devisId, int? paymentTermsId)
        {
            using var cn = Db.Open();

            // 1) Lire total et remise pour base de calcul
            decimal total = 0m;
            decimal remise = 0m;
            using (var c1 = cn.CreateCommand())
            {
                c1.CommandText = "SELECT COALESCE(Total,0), COALESCE(RemiseGlobale,0) FROM Devis WHERE Id=@id;";
                Db.AddParam(c1, "@id", devisId);
                using var rd = c1.ExecuteReader();
                if (rd.Read())
                {
                    total = Convert.ToDecimal(rd.GetValue(0), CultureInfo.InvariantCulture);
                    remise = Convert.ToDecimal(rd.GetValue(1), CultureInfo.InvariantCulture);
                }
            }

            // 2) Charger le modèle choisi (ou null)
            PaymentTerm? pt = null;
            if (paymentTermsId.HasValue)
            {
                var catalogs = new SettingsCatalogService();
                pt = catalogs.GetPaymentTermById(paymentTermsId.Value);
            }

            // 3) Construire texte + plan JSON
            string? txt = null;
            string? planJson = null;

            if (pt == null)
            {
                // Rien sélectionné -> vide
            }
            else if (string.Equals(pt.Mode, "SIMPLE", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(pt.SimpleDue, "AT_ORDER", StringComparison.OrdinalIgnoreCase))
                    txt = "Paiement 100 % à la commande";
                else
                    txt = "Paiement 100 % à la livraison";
            }
            else
            {
                var x = pt.OrderPct ?? 50.0;
                var y = 100.0 - x;

                // Montants : basés sur TOTAL (déjà - remise)
                var m1 = Math.Round((decimal)x / 100m * total, 2, MidpointRounding.AwayFromZero);
                var m2 = total - m1;

                txt = $"{x:0.#} % à la commande, {y:0.#} % à la livraison";

                var payload = new
                {
                    currency = "EUR",
                    totalBase = total,
                    steps = new[]
                    {
                new { order = 1, label = "Acompte",  percent = x, amount = (double)m1, due = "A la commande" },
                new { order = 2, label = "Solde",    percent = y, amount = (double)m2, due = "A la livraison" }
            }
                };
                planJson = System.Text.Json.JsonSerializer.Serialize(payload);
            }

            // 4) Ecrire snapshot dans Devis
            using var up = cn.CreateCommand();
            up.CommandText = @"
UPDATE Devis SET
  PaymentTermsId   = @tid,
  PaymentTermsText = @txt,
  PaymentPlanJson  = @plan
WHERE Id=@id;";
            Db.AddParam(up, "@tid", (object?)paymentTermsId ?? DBNull.Value);
            Db.AddParam(up, "@txt", (object?)txt ?? DBNull.Value);
            Db.AddParam(up, "@plan", (object?)planJson ?? DBNull.Value);
            Db.AddParam(up, "@id", devisId);
            up.ExecuteNonQuery();
        }

        // Compte bancaire
        public void SetBankAccount(int devisId, int? bankAccountId)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "UPDATE Devis SET BankAccountId=@b WHERE Id=@id;";
            Db.AddParam(cmd, "@b", (object?)bankAccountId ?? DBNull.Value);
            Db.AddParam(cmd, "@id", devisId);
            cmd.ExecuteNonQuery();
        }

        // DEVIS Supression
        public void SoftDelete(int id)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "UPDATE Devis SET DeletedAt=@ts WHERE Id=@id;";
            Db.AddParam(cmd, "@ts", DateTime.Now.ToString("s", CultureInfo.InvariantCulture));
            Db.AddParam(cmd, "@id", id);
            cmd.ExecuteNonQuery();
        }
        [SupportedOSPlatform("windows")]
        public string Emit(int devisId, INumberingService numbering)
        {
            if (devisId <= 0) throw new ArgumentException("devisId invalide");

            using var cn = Db.Open();
            using var tr = cn.BeginTransaction();

            // --- 1) Relire le devis (pour savoir s’il a déjà un numéro)
            using var cmdGet = cn.CreateCommand();
            cmdGet.Transaction = tr;
            cmdGet.CommandText = @"
        SELECT Id, Numero, Date,
            ClientSociete, ClientNomPrenom,
            ClientAdresseL1, ClientCodePostal, ClientVille,
            ClientTelephone, ClientEmail
        FROM Devis
        WHERE Id = @id;";
            Db.AddParam(cmdGet, "@id", devisId);
            using var rd = cmdGet.ExecuteReader();
            if (!rd.Read()) throw new InvalidOperationException("Devis introuvable");
            var numeroActuel = rd["Numero"] as string;

            // --- 2) Numéro si absent (format & séquence via NumberingService)
            string numero;
            if (string.IsNullOrWhiteSpace(numeroActuel))
            {
                var today = DateOnly.FromDateTime(DateTime.Now);
                numero = numbering.Next("DEVI", today);
                using var cmdNum = cn.CreateCommand();
                cmdNum.Transaction = tr;
                cmdNum.CommandText = "UPDATE Devis SET Numero=@n, Etat='Envoye', Date=@d WHERE Id=@id;";
                Db.AddParam(cmdNum, "@n", numero);
                Db.AddParam(cmdNum, "@d", DateTime.Now);
                Db.AddParam(cmdNum, "@id", devisId);
                cmdNum.ExecuteNonQuery();
            }
            else
            {
                numero = numeroActuel!;
                using var cmdEtat = cn.CreateCommand();
                cmdEtat.Transaction = tr;
                cmdEtat.CommandText = "UPDATE Devis SET Etat='Envoye' WHERE Id=@id;";
                Db.AddParam(cmdEtat, "@id", devisId);
                cmdEtat.ExecuteNonQuery();
            }

            // --- 3) Relire les infos client & lignes pour construire le PDF
            string clientSociete = rd["ClientSociete"] as string ?? "";
            string clientNomPrenom = rd["ClientNomPrenom"] as string ?? "";
            decimal remiseEuro = 0m;
            {
                using var cmdR = cn.CreateCommand();
                cmdR.Transaction = tr;
                cmdR.CommandText = "SELECT RemiseGlobale FROM Devis WHERE Id=@id;";
                Db.AddParam(cmdR, "@id", devisId);
                var o = cmdR.ExecuteScalar();
                remiseEuro = (o == null || o is DBNull) ? 0m : Convert.ToDecimal(o, CultureInfo.InvariantCulture);
            }
            string clientAddr1 = rd["ClientAdresseL1"] as string ?? "";
            string clientCP = rd["ClientCodePostal"] as string ?? "";
            string clientVille = rd["ClientVille"] as string ?? "";
            string clientTelephone = rd["ClientTelephone"] as string ?? "";
            string clientEmail = rd["ClientEmail"] as string ?? "";
            var phone = clientTelephone;
            var email = clientEmail;
            rd.Close();

            // --- 3a) Lire PaymentTermsText + PaymentPlanJson
            string? payText = null;
            string? payPlanJson = null;
            using (var cmdPT = cn.CreateCommand())
            {
                cmdPT.Transaction = tr;
                cmdPT.CommandText = "SELECT PaymentTermsText, PaymentPlanJson FROM Devis WHERE Id=@id;";
                Db.AddParam(cmdPT, "@id", devisId);
                using var rp = cmdPT.ExecuteReader();
                if (rp.Read())
                {
                    payText = rp["PaymentTermsText"] as string;
                    payPlanJson = rp["PaymentPlanJson"] as string;
                }
            }

            // --- 3b) Lire le compte bancaire éventuellement attaché au devis
            string? bankHolder = null, bankName = null, iban = null, bic = null;

            int? bankId = null;
            using (var cmdB = cn.CreateCommand())
            {
                cmdB.Transaction = tr;
                cmdB.CommandText = "SELECT BankAccountId FROM Devis WHERE Id=@id;";
                Db.AddParam(cmdB, "@id", devisId);
                var o = cmdB.ExecuteScalar();
                if (o != null && o != DBNull.Value)
                    bankId = Convert.ToInt32(o, CultureInfo.InvariantCulture);
            }

            if (bankId.HasValue)
            {
                using var cmdInfo = cn.CreateCommand();
                cmdInfo.Transaction = tr;
                cmdInfo.CommandText = @"
        SELECT Holder, BankName, Iban, Bic
        FROM BankAccounts
        WHERE Id=@bid;";
                Db.AddParam(cmdInfo, "@bid", bankId.Value);
                using var rb = cmdInfo.ExecuteReader();
                if (rb.Read())
                {
                    bankHolder = rb["Holder"] as string;
                    bankName = rb["BankName"] as string;
                    iban = rb["Iban"] as string;
                    bic = rb["Bic"] as string;
                }
            }

            // Lignes
            var lines = new List<(string designation, double qty, double pu)>();
            using (var cmdL = cn.CreateCommand())
            {
                cmdL.Transaction = tr;
                cmdL.CommandText = @"
            SELECT Designation, Qty, PU
            FROM DevisLignes
            WHERE DevisId = @id
            ORDER BY Id;";
                Db.AddParam(cmdL, "@id", devisId);
                using var rl = cmdL.ExecuteReader();
                while (rl.Read())
                {
                    var d = rl["Designation"] as string ?? "";
                    var q = Convert.ToDouble(rl["Qty"], System.Globalization.CultureInfo.InvariantCulture);
                    var p = Convert.ToDouble(rl["PU"], System.Globalization.CultureInfo.InvariantCulture);
                    lines.Add((d, q, p));
                }
            }
            // lire les notes
            string? noteTop = null, noteBottom = null;
            using (var cmdN = cn.CreateCommand())
            {
                cmdN.Transaction = tr;
                cmdN.CommandText = "SELECT NoteHaut, NoteBas FROM Devis WHERE Id=@id;";
                Db.AddParam(cmdN, "@id", devisId);
                using var rn = cmdN.ExecuteReader();
                if (rn.Read()) { noteTop = rn["NoteHaut"] as string; noteBottom = rn["NoteBas"] as string; }
            }

            // Bloc destinataire : Société si présente, sinon Nom Prénom
            var clientName = !string.IsNullOrWhiteSpace(clientSociete)
                ? clientSociete
                : (!string.IsNullOrWhiteSpace(clientNomPrenom) ? clientNomPrenom : "Destinataire non renseigné");

            var clientAddress = string.Join("\n", new[]
            {
                clientAddr1,
                $"{clientCP} {clientVille}".Trim()
            }.Where(s => !string.IsNullOrWhiteSpace(s)));

            // --- 4) Dossier et chemin de sortie
            var outDir = System.IO.Path.Combine(Paths.DataDir, "Devis"); // portable OK
            System.IO.Directory.CreateDirectory(outDir);
            var outfile = System.IO.Path.Combine(outDir, $"{numero}.pdf");

            // --- 5) Génération PDF (v1 micro — mention 293B auto côté modèle)
            // InvoicePdf.RenderSimpleInvoice(outfile, number, clientName, clientAddress, lines, showMention293B)
            // cf. InvoicePdf.cs

            InvoicePdf.RenderSimpleInvoice(
                outputPath: outfile,
                    numero: numero,
                    clientName: clientName,
                    clientAddr: clientAddress,
                    lines: lines,
                    showMention293B: true,
                    discountEuro: remiseEuro,
                    bankHolder: bankHolder,
                    bankName: bankName,
                    iban: iban,
                    bic: bic,
                    paymentTermsText: payText,
                    paymentPlanJson: payPlanJson,
                    noteTop: noteTop,
                    noteBottom: noteBottom,
                    clientPhone: phone,
                    clientEmail: email,
                    devisId: devisId
            );

            tr.Commit();

            return outfile;
        }

        // Regénération de PDF
        [SupportedOSPlatform("windows")]
        public string RegeneratePdf(int devisId)
        {
            if (devisId <= 0) throw new ArgumentException("devisId invalide");

            using var cn = Db.Open();

            // -- Lire snapshot devis (on ne modifie rien)
            using var cmdGet = cn.CreateCommand();
            cmdGet.CommandText = @"
        SELECT Id, Numero, Date,
               ClientSociete, ClientNomPrenom,
               ClientAdresseL1, ClientCodePostal, ClientVille,
               ClientTelephone, ClientEmail,
               RemiseGlobale, PaymentTermsText, PaymentPlanJson,
               BankAccountId, NoteHaut, NoteBas
        FROM Devis
        WHERE Id = @id;";
            Db.AddParam(cmdGet, "@id", devisId);

            string numero, clientSociete, clientNomPrenom, addr1, cp, ville, phone, email;
            decimal remiseEuro;
            string? payText, payPlanJson, noteTop, noteBottom;
            int? bankId = null;

            using (var rd = cmdGet.ExecuteReader())
            {
                if (!rd.Read()) throw new InvalidOperationException("Devis introuvable");
                numero = rd["Numero"] as string ?? "";         // doit exister pour régénérer
                clientSociete = rd["ClientSociete"] as string ?? "";
                clientNomPrenom = rd["ClientNomPrenom"] as string ?? "";
                addr1 = rd["ClientAdresseL1"] as string ?? "";
                cp = rd["ClientCodePostal"] as string ?? "";
                ville = rd["ClientVille"] as string ?? "";
                phone = rd["ClientTelephone"] as string ?? "";
                email = rd["ClientEmail"] as string ?? "";
                remiseEuro = Convert.ToDecimal(rd["RemiseGlobale"] ?? 0m, System.Globalization.CultureInfo.InvariantCulture);
                payText = rd["PaymentTermsText"] as string;
                payPlanJson = rd["PaymentPlanJson"] as string;
                noteTop = rd["NoteHaut"] as string;
                noteBottom = rd["NoteBas"] as string;
                bankId = rd["BankAccountId"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["BankAccountId"]);
            }

            // Lignes
            var lines = new List<(string designation, double qty, double pu)>();
            using (var cmdL = cn.CreateCommand())
            {
                cmdL.CommandText = @"SELECT Designation, Qty, PU FROM DevisLignes WHERE DevisId=@id ORDER BY Id;";
                Db.AddParam(cmdL, "@id", devisId);
                using var rl = cmdL.ExecuteReader();
                while (rl.Read())
                {
                    var d = rl["Designation"] as string ?? "";
                    var q = Convert.ToDouble(rl["Qty"], System.Globalization.CultureInfo.InvariantCulture);
                    var p = Convert.ToDouble(rl["PU"], System.Globalization.CultureInfo.InvariantCulture);
                    lines.Add((d, q, p));
                }
            }

            // Bank (facultatif)
            string? bankHolder = null, bankName = null, iban = null, bic = null;
            if (bankId.HasValue)
            {
                using var cmdB = cn.CreateCommand();
                cmdB.CommandText = @"SELECT Holder, BankName, Iban, Bic FROM BankAccounts WHERE Id=@bid;";
                Db.AddParam(cmdB, "@bid", bankId.Value);
                using var rb = cmdB.ExecuteReader();
                if (rb.Read())
                {
                    bankHolder = rb["Holder"] as string;
                    bankName = rb["BankName"] as string;
                    iban = rb["Iban"] as string;
                    bic = rb["Bic"] as string;
                }
            }

            // Nom/Adresse destinataire
            var clientName = !string.IsNullOrWhiteSpace(clientSociete) ? clientSociete
                            : (!string.IsNullOrWhiteSpace(clientNomPrenom) ? clientNomPrenom : "Destinataire non renseigné");
            var clientAddress = string.Join("\n", new[]
            {
        addr1, $"{cp} {ville}".Trim()
    }.Where(s => !string.IsNullOrWhiteSpace(s)));

            // Chemin de sortie (même nom)
            var outDir = System.IO.Path.Combine(Paths.DataDir, "Devis");
            System.IO.Directory.CreateDirectory(outDir);
            var outfile = System.IO.Path.Combine(outDir, $"{numero}.pdf");

            // Rendu (écrase si existe) + annexes/options pris via 'devisId'
            InvoicePdf.RenderSimpleInvoice(
                outputPath: outfile,
                numero: numero,
                clientName: clientName,
                clientAddr: clientAddress,
                lines: lines,
                showMention293B: true,
                discountEuro: remiseEuro,
                bankHolder: bankHolder,
                bankName: bankName,
                iban: iban,
                bic: bic,
                paymentTermsText: payText,
                paymentPlanJson: payPlanJson,
                noteTop: noteTop,
                noteBottom: noteBottom,
                clientPhone: phone,
                clientEmail: email,
                devisId: devisId
            );

            return outfile;
        }


        public void MarkTransformed(int devisId)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "UPDATE Devis SET Etat='Transforme' WHERE Id=@d;";
            Db.AddParam(cmd, "@d", devisId);
            cmd.ExecuteNonQuery();
        }

        public List<Devis> GetAll(string? search = null)
        {
            var list = new List<Devis>();
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            if (string.IsNullOrWhiteSpace(search))
                cmd.CommandText = "SELECT * FROM Devis WHERE DeletedAt IS NULL ORDER BY Date DESC, Id DESC;";
            else
            {
                cmd.CommandText = @"SELECT * FROM Devis
WHERE DeletedAt IS NULL AND (Numero LIKE @q OR ClientNom LIKE @q)
ORDER BY Date DESC, Id DESC;";
                Db.AddParam(cmd, "@q", "%" + search + "%");
            }
            using var rd = cmd.ExecuteReader();
            while (rd.Read()) list.Add(MapDevis(rd));
            return list;
        }

        public Devis? GetById(int id)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT * FROM Devis WHERE Id=@id;";
            Db.AddParam(cmd, "@id", id);
            using var rd = cmd.ExecuteReader();
            return rd.Read() ? MapDevis(rd) : null;
        }

        public List<DevisLigne> GetLines(int devisId)
        {
            var list = new List<DevisLigne>();
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT * FROM DevisLignes WHERE DevisId=@d ORDER BY Id;";
            Db.AddParam(cmd, "@d", devisId);
            using var rd = cmd.ExecuteReader();
            while (rd.Read()) list.Add(MapLigne(rd));
            return list;
        }

        public List<DevisAnnexe> GetAnnexes(int devisId)
        {
            var list = new List<DevisAnnexe>();
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT * FROM DevisAnnexes WHERE DevisId=@d ORDER BY Ordre, Id;";
            Db.AddParam(cmd, "@d", devisId);
            using var rd = cmd.ExecuteReader();
            while (rd.Read()) list.Add(MapAnnexe(rd));
            return list;
        }

        // Options sur devis
        public int AddOptionFromArticle(int devisId, int articleId, int? varianteId = null)
        {
            var artSvc = new ArticleService();

            // 1) lire article / variante (packs inclus via variantes dans le picker)
            var art = artSvc.GetById(articleId);
            if (art == null) throw new InvalidOperationException("Article introuvable");

            string libelle;
            decimal prix;

            if (varianteId.HasValue)
            {
                var v = artSvc.GetVariantById(varianteId.Value);
                if (v == null) throw new InvalidOperationException("Variante introuvable");
                libelle = string.IsNullOrWhiteSpace(v.Nom) ? art.Libelle ?? $"Article #{art.Id}" : $"{art.Libelle} {v.Nom}";
                prix = v.PrixVenteHT;
            }
            else
            {
                libelle = art.Libelle ?? $"Article #{art.Id}";
                prix = art.PrixVenteHT;
            }

            // 2) snapshot en BDD
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
INSERT INTO DevisOptions(DevisId, ArticleId, VarianteId, Libelle, Prix)
VALUES(@D,@A,@V,@L,@P);
SELECT last_insert_rowid();";
            Db.AddParam(cmd, "@D", devisId);
            Db.AddParam(cmd, "@A", (object?)articleId ?? DBNull.Value);
            Db.AddParam(cmd, "@V", (object?)varianteId ?? DBNull.Value);
            Db.AddParam(cmd, "@L", libelle);
            Db.AddParam(cmd, "@P", prix);
            return Convert.ToInt32(cmd.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
        }

        public List<DevisOption> GetOptions(int devisId)
        {
            var list = new List<DevisOption>();
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT Id, DevisId, ArticleId, VarianteId, Libelle, Prix FROM DevisOptions WHERE DevisId=@D ORDER BY Id;";
            Db.AddParam(cmd, "@D", devisId);
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                list.Add(new DevisOption
                {
                    Id = Convert.ToInt32(rd["Id"]),
                    DevisId = Convert.ToInt32(rd["DevisId"]),
                    ArticleId = rd["ArticleId"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["ArticleId"]),
                    VarianteId = rd["VarianteId"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["VarianteId"]),
                    Libelle = rd["Libelle"]?.ToString() ?? "",
                    Prix = Convert.ToDecimal(rd["Prix"], System.Globalization.CultureInfo.InvariantCulture)
                });
            }
            return list;
        }

        public void DeleteOption(int optionId)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "DELETE FROM DevisOptions WHERE Id=@Id;";
            Db.AddParam(cmd, "@Id", optionId);
            cmd.ExecuteNonQuery();
        }

        // Corection soucis de nom de service
        public void UpdateHeaderFields(int devisId, string? titre, string? noteHaut, string? noteBas, decimal? remiseGlobale)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
UPDATE Devis
SET
  NoteHaut       = @h,
  NoteBas        = @b,
  RemiseGlobale  = COALESCE(@r, RemiseGlobale)
WHERE Id=@id;";
            Db.AddParam(cmd, "@h", (object?)noteHaut ?? DBNull.Value);
            Db.AddParam(cmd, "@b", (object?)noteBas ?? DBNull.Value);
            Db.AddParam(cmd, "@r", (object?)remiseGlobale ?? DBNull.Value);
            Db.AddParam(cmd, "@id", devisId);
            cmd.ExecuteNonQuery();

            if (remiseGlobale.HasValue)
                RecalcTotals(devisId);
        }

        // ----- Depuis la vue clients -----
        public List<Devis> GetByClient(int clientId)
        {
            var result = new List<Devis>();
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
        SELECT *
        FROM Devis
        WHERE DeletedAt IS NULL
          AND ClientId=@cid
        ORDER BY Date DESC, Id DESC;";
            Db.AddParam(cmd, "@cid", clientId);
            using var rd = cmd.ExecuteReader();
            while (rd.Read()) result.Add(MapDevis(rd));
            return result;
        }

        // ----- mappers -----
        public void SetRemiseGlobale(int devisId, decimal remiseGlobale)
            => SetGlobalDiscount(devisId, remiseGlobale);
        public List<Devis> Search(string? q) => GetAll(q);
        public int CreateDraft() => CreateDraft(null, null);
        private static Devis MapDevis(IDataRecord r) => new Devis
        {
            Id = Convert.ToInt32(r["Id"]),
            Numero = r["Numero"]?.ToString(),
            Date = DateOnly.ParseExact(r["Date"]?.ToString() ?? DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd"), "yyyy-MM-dd", CultureInfo.InvariantCulture),
            Etat = r["Etat"]?.ToString() ?? "Brouillon",

            ClientId = r["ClientId"] == DBNull.Value ? null : Convert.ToInt32(r["ClientId"]),
            ClientSociete = r["ClientSociete"]?.ToString(),
            ClientNomPrenom = r["ClientNomPrenom"]?.ToString(),
            ClientNom = r["ClientNom"]?.ToString(),               // (historique, si tu l’utilises ailleurs)
            ClientEmail = r["ClientEmail"]?.ToString(),
            ClientTelephone = r["ClientTelephone"]?.ToString(),
            ClientAdresseL1 = r["ClientAdresseL1"]?.ToString(),
            ClientCodePostal = r["ClientCodePostal"]?.ToString(),
            ClientVille = r["ClientVille"]?.ToString(),
            NoteHaut = r["NoteHaut"]?.ToString(),
            NoteBas = r["NoteBas"]?.ToString(),
            RemiseGlobale = Convert.ToDecimal(r["RemiseGlobale"], CultureInfo.InvariantCulture),
            Total = Convert.ToDecimal(r["Total"], CultureInfo.InvariantCulture),
            BankAccountId = r["BankAccountId"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["BankAccountId"]),
            PaymentTermsId = r["PaymentTermsId"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["PaymentTermsId"]),
            PaymentTermsText = r["PaymentTermsText"]?.ToString(),
            PaymentPlanJson = r["PaymentPlanJson"]?.ToString(),
            DeletedAt = r["DeletedAt"] == DBNull.Value ? (DateTime?)null : DateTime.Parse(r["DeletedAt"].ToString()!)
        };

        private static DevisLigne MapLigne(IDataRecord r) => new DevisLigne
        {
            Id = Convert.ToInt32(r["Id"]),
            DevisId = Convert.ToInt32(r["DevisId"]),
            Designation = r["Designation"]?.ToString() ?? "",
            Qty = Convert.ToDecimal(r["Qty"], CultureInfo.InvariantCulture),
            PU = Convert.ToDecimal(r["PU"], CultureInfo.InvariantCulture),
            Montant = Convert.ToDecimal(r["Montant"], CultureInfo.InvariantCulture),
            ArticleId = r["ArticleId"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["ArticleId"]),
            VarianteId = r["VarianteId"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["VarianteId"]),
            ImagePath = r["ImagePath"]?.ToString()
        };

        private static DevisAnnexe MapAnnexe(IDataRecord r) => new DevisAnnexe
        {
            Id = Convert.ToInt32(r["Id"]),
            DevisId = Convert.ToInt32(r["DevisId"]),
            Type = r["Type"]?.ToString() ?? "PDF",
            Chemin = r["Chemin"]?.ToString(),
            Ordre = Convert.ToInt32(r["Ordre"]),
            ConfigJson = r["ConfigJson"]?.ToString()
        };

        public void RemoveAnnexe(int annexeId)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "DELETE FROM DevisAnnexes WHERE Id=@id;";
            Db.AddParam(cmd, "@id", annexeId);
            cmd.ExecuteNonQuery();
        }

        // Gestion statut du devis 
        public void MarkEmailSent(int devisId)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "UPDATE Devis SET SentAt=@ts WHERE Id=@id;";
            Db.AddParam(cmd, "@ts", DateTime.Now.ToString("s"));
            Db.AddParam(cmd, "@id", devisId);
            cmd.ExecuteNonQuery();
        }

        public string EmitNumero(int devisId, INumberingService num)
        {
            using var cn = Db.Open();

            // 1) Lire l'état actuel (Numero + Date) SANS transaction d'écriture
            using var get = cn.CreateCommand();

            get.CommandText = "SELECT Numero, Date FROM Devis WHERE Id=@id;";
            Db.AddParam(get, "@id", devisId);

            string? currentNumero = null;
            DateOnly dateDevis = DateOnly.FromDateTime(DateTime.Today);

            using (var rd = get.ExecuteReader())
            {
                if (!rd.Read())
                    throw new InvalidOperationException("Devis introuvable.");

                currentNumero = rd.IsDBNull(0) ? null : rd.GetString(0);

                if (!rd.IsDBNull(1))
                {
                    var s = rd.GetValue(1)?.ToString();
                    if (!string.IsNullOrWhiteSpace(s) && DateOnly.TryParse(s, out var d))
                        dateDevis = d;
                }
            }

            // 2) Déjà numéroté ? on renvoie
            if (!string.IsNullOrWhiteSpace(currentNumero))
                return currentNumero!;

            // 3) Numérotation (sa transaction à elle)
            var newNumero = num.Next("DEVI", dateDevis);

            // 4) Ecriture du numéro dans Devis (transaction courte, maintenant seulement)
            using var tx = cn.BeginTransaction();
            using (var upd = cn.CreateCommand())
            {
                upd.Transaction = tx;
                upd.CommandText = "UPDATE Devis SET Numero=@n, Etat='Numerote' WHERE Id=@id;";
                Db.AddParam(upd, "@n", newNumero);
                Db.AddParam(upd, "@id", devisId);
                upd.ExecuteNonQuery();
            }
            tx.Commit();

            return newNumero;
        }
    }
}
