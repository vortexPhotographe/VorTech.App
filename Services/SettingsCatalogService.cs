using System;
using System.Collections.Generic;
using System.Linq;
using VorTech.App.Models;

namespace VorTech.App.Services
{
    public class SettingsCatalogService
    {
        public SettingsCatalogService() => EnsureSchema();

        private void EnsureSchema()
        {
            using var cn = Db.Open();

            // CotisationTypes
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS CotisationTypes(
  Id   INTEGER PRIMARY KEY AUTOINCREMENT,
  Name TEXT    NOT NULL,
  Rate REAL    NOT NULL DEFAULT 0
);";
                cmd.ExecuteNonQuery();
            }

            // TvaRates
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS TvaRates(
  Id        INTEGER PRIMARY KEY AUTOINCREMENT,
  Name      TEXT    NOT NULL,
  Rate      REAL    NOT NULL DEFAULT 0,
  IsDefault INTEGER NOT NULL DEFAULT 0
);";
                cmd.ExecuteNonQuery();
            }

            // Categories
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS Categories(
  Id    INTEGER PRIMARY KEY AUTOINCREMENT,
  Name  TEXT NOT NULL,
  Actif INTEGER DEFAULT 1
);";
                cmd.ExecuteNonQuery();
            }

            // --- CompanyProfile : 1 seule ligne ---
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS CompanyProfile(
              Id            INTEGER PRIMARY KEY CHECK (Id=1),
              NomCommercial TEXT    NOT NULL DEFAULT '',
              Siret         TEXT    NOT NULL DEFAULT '',
              Adresse1      TEXT    NOT NULL DEFAULT '',
              Adresse2      TEXT    NOT NULL DEFAULT '',
              CodePostal    TEXT    NOT NULL DEFAULT '',
              Ville         TEXT    NOT NULL DEFAULT '',
              Pays          TEXT    NOT NULL DEFAULT '',
              Email         TEXT    NOT NULL DEFAULT '',
              Telephone     TEXT    NOT NULL DEFAULT '',
              SiteWeb       TEXT    NOT NULL DEFAULT ''
            );";
                cmd.ExecuteNonQuery();
            }

            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS PaymentTerms(
  Id         INTEGER PRIMARY KEY AUTOINCREMENT,
  Name       TEXT    NOT NULL,
  Mode       TEXT    NOT NULL,           -- 'SIMPLE' | 'DUAL'
  SimpleDue  TEXT    NULL,               -- 'AT_ORDER' | 'AT_DELIVERY' (si SIMPLE)
  OrderPct   REAL    NULL,               -- 0..100 (si DUAL)
  IsDefault  INTEGER NOT NULL DEFAULT 0, -- 0/1
  Body       TEXT    NOT NULL            -- texte lisible
);";
                cmd.ExecuteNonQuery();
            }

            // Seed 1 ligne si vide
            using (var check = cn.CreateCommand())
            {
                check.CommandText = "SELECT COUNT(*) FROM CompanyProfile;";
                var count = Convert.ToInt32(check.ExecuteScalar() ?? 0);
                if (count == 0)
                {
                    using var ins = cn.CreateCommand();
                    ins.CommandText = @"
INSERT INTO CompanyProfile(Id, NomCommercial, Siret, Adresse1, Adresse2, CodePostal, Ville, Pays, Email, Telephone, SiteWeb)
VALUES(1, '', '', '', '', '', '', '', '', '', '');";
                    ins.ExecuteNonQuery();
                }
            }

            // --- Compte Emails ---
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS EmailAccounts (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  DisplayName TEXT NOT NULL,
  Address     TEXT NOT NULL,
  SmtpHost    TEXT NOT NULL,
  SmtpPort    INTEGER NOT NULL,
  UseSsl      INTEGER NOT NULL DEFAULT 1,
  Username    TEXT NOT NULL,
  Password    TEXT NOT NULL,
  IsDefault   INTEGER NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX IF NOT EXISTS UX_EmailAccounts_Address ON EmailAccounts(Address);";
                cmd.ExecuteNonQuery();
            }

            // --- Modele Email ---
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS EmailTemplates (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  Name TEXT NOT NULL,                -- ex: ""Envoi devis""
  Subject TEXT NOT NULL,            -- ex: ""Votre devis {{Devis.Numero}}""
  Body TEXT NOT NULL,               -- HTML ou texte (on gère les deux)
  IsHtml INTEGER NOT NULL DEFAULT 1
);";
                cmd.ExecuteNonQuery();
            }

            // --- BankAccounts ----
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS BankAccounts(
  Id          INTEGER PRIMARY KEY AUTOINCREMENT,
  DisplayName TEXT    NOT NULL,   -- ex: 'Compte Pro Société Générale'
  Iban        TEXT    NOT NULL,
  Bic         TEXT    NOT NULL,
  Holder      TEXT    NOT NULL,   -- Titulaire
  BankName    TEXT    NOT NULL,   -- Nom de la banque
  IsDefault   INTEGER NOT NULL DEFAULT 0
);";
                cmd.ExecuteNonQuery();
            }

            // --- Annexes (catalogue) ---
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS Annexes(
  Id            INTEGER PRIMARY KEY AUTOINCREMENT,
  Nom           TEXT    NOT NULL,
  CheminRelatif TEXT    NOT NULL,   -- ex: 'Annexes/ma-brochure.pdf'
  Actif         INTEGER NOT NULL DEFAULT 1
);";
                cmd.ExecuteNonQuery();
            }

            // --- PaymentModes (Moyens de paiement ---
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS PaymentModes(
  Id        INTEGER PRIMARY KEY AUTOINCREMENT,
  Name      TEXT    NOT NULL,
  FeeFixed  REAL    NOT NULL DEFAULT 0,
  FeeRate   REAL    NOT NULL DEFAULT 0,
  IsActive  INTEGER NOT NULL DEFAULT 1
);";
                cmd.ExecuteNonQuery();
            }

            // Seed minimal si table vide
            using (var check = cn.CreateCommand())
            {
                check.CommandText = "SELECT COUNT(*) FROM PaymentModes;";
                var count = Convert.ToInt32(check.ExecuteScalar() ?? 0);
                if (count == 0)
                {
                    using var ins = cn.CreateCommand();
                    ins.CommandText = @"
INSERT INTO PaymentModes(Name, FeeFixed, FeeRate, IsActive)
VALUES
 ('Espèces', 0, 0, 1),
 ('Virement', 0, 0, 1),
 ('Carte bancaire', 0, 1.2, 1);";
                    ins.ExecuteNonQuery();
                }
            }

            // Seed TVA si vide
            using (var check = cn.CreateCommand())
            {
                check.CommandText = "SELECT COUNT(*) FROM TvaRates;";
                var count = Convert.ToInt32(check.ExecuteScalar() ?? 0);
                if (count == 0)
                {
                    void ins(string name, double rate, int isDefault)
                    {
                        using var insCmd = cn.CreateCommand();
                        insCmd.CommandText = "INSERT INTO TvaRates(Name, Rate, IsDefault) VALUES($n,$r,$d);";
                        insCmd.Parameters.AddWithValue("$n", name);
                        insCmd.Parameters.AddWithValue("$r", rate);
                        insCmd.Parameters.AddWithValue("$d", isDefault);
                        insCmd.ExecuteNonQuery();
                    }
                    ins("0%", 0.0, 1);
                    ins("Taux normal", 20.0, 0);
                    ins("Taux intermédiaire", 10.0, 0);
                    ins("Taux réduit", 5.5, 0);
                    ins("Taux particulier", 2.1, 0);
                }
            }

            // Seed Catégorie par défaut si vide
            using (var check = cn.CreateCommand())
            {
                check.CommandText = "SELECT COUNT(*) FROM Categories;";
                var count = Convert.ToInt32(check.ExecuteScalar() ?? 0);
                if (count == 0)
                {
                    using var ins = cn.CreateCommand();
                    ins.CommandText = "INSERT INTO Categories(Name, Actif) VALUES('Général',1);";
                    ins.ExecuteNonQuery();
                }
            }

            // -- Numérotation Factures & Avoirs : FACT-{yyyy}-{MM}-{####} / AVOIR-{yyyy}-{MM}-{####}
            {
                var num = new NumberingService();
                // "MONTHLY" = compteur par mois (clé période = AAAA-MM)
                num.SetFormat("FACT", "FACT-{yyyy}-{MM}-{####}", "MONTHLY");
                num.SetFormat("AVOIR", "AVOIR-{yyyy}-{MM}-{####}", "MONTHLY");
            }

            // --- Factures (entête) ---
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS Factures(
  Id                INTEGER PRIMARY KEY AUTOINCREMENT,
  Numero            TEXT UNIQUE,                    -- FACT-YYYY-MM-####
  ClientId          INTEGER,
  Date              TEXT    NOT NULL DEFAULT (DATE('now')), -- ISO YYYY-MM-DD
  Etat              TEXT    NOT NULL DEFAULT 'Brouillon',   -- Brouillon / Cree / Envoye / Payer
  RemiseGlobale     REAL    NOT NULL DEFAULT 0,
  Total             REAL    NOT NULL DEFAULT 0,
  PaymentTermsId    INTEGER NULL,
  SentAt            TEXT    NULL,
  DeletedAt         TEXT    NULL,
  D                 INTEGER NOT NULL DEFAULT 0,      -- drapeau bool (ton 'D' demandé) 0/1
  DevisSourceId     INTEGER NULL,                    -- lien éventuel au devis source
  FOREIGN KEY(ClientId) REFERENCES Clients(Id) ON DELETE SET NULL,
  FOREIGN KEY(PaymentTermsId) REFERENCES PaymentTerms(Id) ON DELETE SET NULL
);";
                cmd.ExecuteNonQuery();
            }

            // --- FactureLignes ---
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS FactureLignes(
  Id           INTEGER PRIMARY KEY AUTOINCREMENT,
  FactureId    INTEGER NOT NULL,
  Designation  TEXT    NOT NULL,
  Qty          REAL    NOT NULL DEFAULT 1,
  PU           REAL    NOT NULL DEFAULT 0,
  Montant      REAL    NOT NULL DEFAULT 0,
  ArticleId    INTEGER NULL,
  VarianteId   INTEGER NULL,
  CotisationRateId INTEGER NULL,
  DevisLigneId INTEGER NULL,                         -- traçabilité depuis Devis
  FOREIGN KEY(FactureId) REFERENCES Factures(Id) ON DELETE CASCADE
);";
                cmd.ExecuteNonQuery();
            }

            // --- Index utiles ---
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = @"
CREATE INDEX IF NOT EXISTS IX_Factures_Client ON Factures(ClientId);
CREATE INDEX IF NOT EXISTS IX_FactureLignes_Facture ON FactureLignes(FactureId);";
                cmd.ExecuteNonQuery();
            }

            // --- Numérotation 'FACT' : pattern & scope ---
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = @"
INSERT INTO DocNumberingFormats(DocType,Pattern,Scope)
VALUES('FACT','FACT-{yyyy}-{MM}-{####}','MONTHLY')
ON CONFLICT(DocType) DO NOTHING;";
                cmd.ExecuteNonQuery();
            }

            // --- Factures : snapshot client (ajout ssi manquant) ---
            void TryAdd(string column, string sqlType)
            {
                using var chk = cn.CreateCommand();
                chk.CommandText = $"PRAGMA table_info(Factures);";
                using var rd = chk.ExecuteReader();
                var exists = false;
                while (rd.Read())
                    if (string.Equals(rd["name"]?.ToString(), column, StringComparison.OrdinalIgnoreCase))
                    { exists = true; break; }
                if (!exists)
                {
                    using var alt = cn.CreateCommand();
                    alt.CommandText = $"ALTER TABLE Factures ADD COLUMN {column} {sqlType};";
                    alt.ExecuteNonQuery();
                }
            }
            TryAdd("ClientSociete", "TEXT");
            TryAdd("ClientNomPrenom", "TEXT");
            TryAdd("ClientAdresseL1", "TEXT");
            TryAdd("ClientCodePostal", "TEXT");
            TryAdd("ClientVille", "TEXT");
            TryAdd("ClientEmail", "TEXT");
            TryAdd("ClientTelephone", "TEXT");

        }

        public List<CotisationType> GetCotisationTypes()
        {
            var list = new List<CotisationType>();
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, Rate FROM CotisationTypes ORDER BY Name COLLATE NOCASE";
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                list.Add(new CotisationType
                {
                    Id = rd.GetInt32(0),
                    Name = rd.IsDBNull(1) ? "" : rd.GetString(1),
                    Rate = rd.IsDBNull(2) ? 0 : rd.GetDouble(2),
                });
            }
            return list;
        }

        // Alias pour coller à l'usage actuel
        public List<CotisationType> GetCotisationRates() => GetCotisationTypes();

        public List<TvaRate> GetTvaRates()
        {
            var list = new List<TvaRate>();
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, Rate, IsDefault FROM TvaRates ORDER BY Name COLLATE NOCASE";
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                list.Add(new TvaRate
                {
                    Id = rd.GetInt32(0),
                    Name = rd.IsDBNull(1) ? "" : rd.GetString(1),
                    Rate = rd.IsDBNull(2) ? 0 : rd.GetDouble(2),
                    IsDefault = !rd.IsDBNull(3) && rd.GetInt32(3) != 0,
                });
            }
            return list;
        }

        public List<Categorie> GetCategories()
        {
            var list = new List<Categorie>();
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, Actif FROM Categories ORDER BY Name;";
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                list.Add(new Categorie
                {
                    Id = rd.GetInt32(0),
                    Name = rd.IsDBNull(1) ? "" : rd.GetString(1),
                    Actif = !rd.IsDBNull(2) && rd.GetInt32(2) != 0
                });
            }
            return list;
        }

        public decimal GetRateById(int? id)
        {
            if (id == null) return 0m;
            var tva = GetTvaRates().FirstOrDefault(x => x.Id == id)?.Rate ?? 0.0;
            var cot = GetCotisationTypes().FirstOrDefault(x => x.Id == id)?.Rate ?? 0.0;
            var r = tva != 0.0 ? tva : cot;
            return (decimal)r;
        }

        // CRUD
        // TAB  Category
        public int InsertCategory(string name, bool actif)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"INSERT INTO Categories(Name, Actif) VALUES(@n,@a);
                        SELECT last_insert_rowid();";
            Db.AddParam(cmd, "@n", name ?? "");
            Db.AddParam(cmd, "@a", actif ? 1 : 0);
            return Convert.ToInt32(cmd.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
        }

        public void UpdateCategory(int id, string name, bool actif)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "UPDATE Categories SET Name=@n, Actif=@a WHERE Id=@id;";
            Db.AddParam(cmd, "@n", name ?? "");
            Db.AddParam(cmd, "@a", actif ? 1 : 0);
            Db.AddParam(cmd, "@id", id);
            cmd.ExecuteNonQuery();
        }

        public void DeleteCategory(int id)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "DELETE FROM Categories WHERE Id=@id;";
            Db.AddParam(cmd, "@id", id);
            cmd.ExecuteNonQuery();
        }

        // TAB TVA
        public int InsertTvaRate(string name, decimal rate, bool isDefault)
        {
            using var cn = Db.Open();
            using var tx = cn.BeginTransaction();

            if (isDefault)
            {
                using var clear = cn.CreateCommand();
                clear.CommandText = "UPDATE TvaRates SET IsDefault=0;";
                clear.ExecuteNonQuery();
            }

            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"INSERT INTO TvaRates(Name, Rate, IsDefault) VALUES(@n,@r,@d);
                        SELECT last_insert_rowid();";
            Db.AddParam(cmd, "@n", name ?? "");
            Db.AddParam(cmd, "@r", rate);
            Db.AddParam(cmd, "@d", isDefault ? 1 : 0);
            var id = Convert.ToInt32(cmd.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);

            tx.Commit();
            return id;
        }

        public void UpdateTvaRate(int id, string name, decimal rate, bool isDefault)
        {
            using var cn = Db.Open();
            using var tx = cn.BeginTransaction();

            if (isDefault)
            {
                using var clear = cn.CreateCommand();
                clear.CommandText = "UPDATE TvaRates SET IsDefault=0;";
                clear.ExecuteNonQuery();
            }

            using var cmd = cn.CreateCommand();
            cmd.CommandText = "UPDATE TvaRates SET Name=@n, Rate=@r, IsDefault=@d WHERE Id=@id;";
            Db.AddParam(cmd, "@n", name ?? "");
            Db.AddParam(cmd, "@r", rate);
            Db.AddParam(cmd, "@d", isDefault ? 1 : 0);
            Db.AddParam(cmd, "@id", id);
            cmd.ExecuteNonQuery();

            tx.Commit();
        }

        public void DeleteTvaRate(int id)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "DELETE FROM TvaRates WHERE Id=@id;";
            Db.AddParam(cmd, "@id", id);
            cmd.ExecuteNonQuery();
        }

        // TAB Cotisations
        public int InsertCotisationType(string name, decimal rate)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"INSERT INTO CotisationTypes(Name, Rate) VALUES(@n,@r);
                        SELECT last_insert_rowid();";
            Db.AddParam(cmd, "@n", name ?? "");
            Db.AddParam(cmd, "@r", rate);
            return Convert.ToInt32(cmd.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
        }

        public void UpdateCotisationType(int id, string name, decimal rate)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "UPDATE CotisationTypes SET Name=@n, Rate=@r WHERE Id=@id;";
            Db.AddParam(cmd, "@n", name ?? "");
            Db.AddParam(cmd, "@r", rate);
            Db.AddParam(cmd, "@id", id);
            cmd.ExecuteNonQuery();
        }

        public void DeleteCotisationType(int id)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "DELETE FROM CotisationTypes WHERE Id=@id;";
            Db.AddParam(cmd, "@id", id);
            cmd.ExecuteNonQuery();
        }

        // TAB CompanyProfile PAS DE SUPR
        public CompanyProfile GetCompanyProfile()
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
SELECT Id, NomCommercial, Siret, Adresse1, Adresse2, CodePostal, Ville, Pays, Email, Telephone, SiteWeb
FROM CompanyProfile
WHERE Id=1;";
            using var rd = cmd.ExecuteReader();
            if (rd.Read())
            {
                return new CompanyProfile
                {
                    Id = 1,
                    NomCommercial = rd["NomCommercial"]?.ToString() ?? "",
                    Siret = rd["Siret"]?.ToString() ?? "",
                    Adresse1 = rd["Adresse1"]?.ToString() ?? "",
                    Adresse2 = rd["Adresse2"]?.ToString() ?? "",
                    CodePostal = rd["CodePostal"]?.ToString() ?? "",
                    Ville = rd["Ville"]?.ToString() ?? "",
                    Pays = rd["Pays"]?.ToString() ?? "",
                    Email = rd["Email"]?.ToString() ?? "",
                    Telephone = rd["Telephone"]?.ToString() ?? "",
                    SiteWeb = rd["SiteWeb"]?.ToString() ?? ""
                };
            }
            return new CompanyProfile(); // fallback
        }

        public void SaveCompanyProfile(CompanyProfile p)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
UPDATE CompanyProfile SET
  NomCommercial=@n,
  Siret=@s,
  Adresse1=@a1,
  Adresse2=@a2,
  CodePostal=@cp,
  Ville=@v,
  Pays=@pays,
  Email=@mail,
  Telephone=@tel,
  SiteWeb=@web
WHERE Id=1;";
            Db.AddParam(cmd, "@n", p.NomCommercial ?? "");
            Db.AddParam(cmd, "@s", p.Siret ?? "");
            Db.AddParam(cmd, "@a1", p.Adresse1 ?? "");
            Db.AddParam(cmd, "@a2", p.Adresse2 ?? "");
            Db.AddParam(cmd, "@cp", p.CodePostal ?? "");
            Db.AddParam(cmd, "@v", p.Ville ?? "");
            Db.AddParam(cmd, "@pays", p.Pays ?? "");
            Db.AddParam(cmd, "@mail", p.Email ?? "");
            Db.AddParam(cmd, "@tel", p.Telephone ?? "");
            Db.AddParam(cmd, "@web", p.SiteWeb ?? "");
            cmd.ExecuteNonQuery();
        }

        // -------- EMAIL ACCOUNTS --------
        public List<EmailAccount> GetEmailAccounts()
        {
            var list = new List<EmailAccount>();
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"SELECT Id, DisplayName, Address, SmtpHost, SmtpPort, UseSsl, Username, Password, IsDefault FROM EmailAccounts ORDER BY IsDefault DESC, Id;";
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                list.Add(new EmailAccount
                {
                    Id = rd.GetInt32(0),
                    DisplayName = rd.GetString(1),
                    Address = rd.GetString(2),
                    SmtpHost = rd.GetString(3),
                    SmtpPort = rd.GetInt32(4),
                    UseSsl = rd.GetInt32(5) != 0,
                    Username = rd.GetString(6),
                    Password = rd.GetString(7),
                    IsDefault = rd.GetInt32(8) != 0
                });
            }
            return list;
        }

        public int InsertEmailAccount(EmailAccount a)
        {
            using var cn = Db.Open();
            using var tx = cn.BeginTransaction();

            if (a.IsDefault)
            {
                using var clear = cn.CreateCommand();
                clear.CommandText = "UPDATE EmailAccounts SET IsDefault = 0;";
                clear.ExecuteNonQuery();
            }

            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
INSERT INTO EmailAccounts(DisplayName, Address, SmtpHost, SmtpPort, UseSsl, Username, Password, IsDefault)
VALUES(@dn,@ad,@host,@port,@ssl,@usr,@pwd,@def);
SELECT last_insert_rowid();";
            Db.AddParam(cmd, "@dn", a.DisplayName ?? "");
            Db.AddParam(cmd, "@ad", a.Address ?? "");
            Db.AddParam(cmd, "@host", a.SmtpHost ?? "");
            Db.AddParam(cmd, "@port", a.SmtpPort);
            Db.AddParam(cmd, "@ssl", a.UseSsl ? 1 : 0);
            Db.AddParam(cmd, "@usr", a.Username ?? "");
            Db.AddParam(cmd, "@pwd", a.Password ?? "");
            Db.AddParam(cmd, "@def", a.IsDefault ? 1 : 0);
            var id = Convert.ToInt32(cmd.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);

            tx.Commit();
            return id;
        }

        public void UpdateEmailAccount(EmailAccount a)
        {
            using var cn = Db.Open();
            using var tx = cn.BeginTransaction();

            if (a.IsDefault)
            {
                using var clear = cn.CreateCommand();
                clear.CommandText = "UPDATE EmailAccounts SET IsDefault = 0;";
                clear.ExecuteNonQuery();
            }

            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
UPDATE EmailAccounts
SET DisplayName=@dn, Address=@ad, SmtpHost=@host, SmtpPort=@port, UseSsl=@ssl, Username=@usr, Password=@pwd, IsDefault=@def
WHERE Id=@id;";
            Db.AddParam(cmd, "@id", a.Id);
            Db.AddParam(cmd, "@dn", a.DisplayName ?? "");
            Db.AddParam(cmd, "@ad", a.Address ?? "");
            Db.AddParam(cmd, "@host", a.SmtpHost ?? "");
            Db.AddParam(cmd, "@port", a.SmtpPort);
            Db.AddParam(cmd, "@ssl", a.UseSsl ? 1 : 0);
            Db.AddParam(cmd, "@usr", a.Username ?? "");
            Db.AddParam(cmd, "@pwd", a.Password ?? "");
            Db.AddParam(cmd, "@def", a.IsDefault ? 1 : 0);
            cmd.ExecuteNonQuery();

            tx.Commit();
        }

        public void DeleteEmailAccount(int id)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "DELETE FROM EmailAccounts WHERE Id=@id;";
            Db.AddParam(cmd, "@id", id);
            cmd.ExecuteNonQuery();
        }

        // -------- EMAIL TEMPLATES --------
        public List<EmailTemplate> GetEmailTemplates()
        {
            var list = new List<EmailTemplate>();
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"SELECT Id, Name, Subject, Body, IsHtml FROM EmailTemplates ORDER BY Id;";
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                list.Add(new EmailTemplate
                {
                    Id = rd.GetInt32(0),
                    Name = rd.GetString(1),
                    Subject = rd.GetString(2),
                    Body = rd.GetString(3),
                    IsHtml = rd.GetInt32(4) != 0
                });
            }
            return list;
        }

        public int InsertEmailTemplate(EmailTemplate t)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
INSERT INTO EmailTemplates(Name, Subject, Body, IsHtml)
VALUES(@n,@s,@b,@h);
SELECT last_insert_rowid();";
            Db.AddParam(cmd, "@n", t.Name ?? "");
            Db.AddParam(cmd, "@s", t.Subject ?? "");
            Db.AddParam(cmd, "@b", t.Body ?? "");
            Db.AddParam(cmd, "@h", t.IsHtml ? 1 : 0);
            return Convert.ToInt32(cmd.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
        }

        public void UpdateEmailTemplate(EmailTemplate t)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
UPDATE EmailTemplates
SET Name=@n, Subject=@s, Body=@b, IsHtml=@h
WHERE Id=@id;";
            Db.AddParam(cmd, "@id", t.Id);
            Db.AddParam(cmd, "@n", t.Name ?? "");
            Db.AddParam(cmd, "@s", t.Subject ?? "");
            Db.AddParam(cmd, "@b", t.Body ?? "");
            Db.AddParam(cmd, "@h", t.IsHtml ? 1 : 0);
            cmd.ExecuteNonQuery();
        }

        public void DeleteEmailTemplate(int id)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "DELETE FROM EmailTemplates WHERE Id=@id;";
            Db.AddParam(cmd, "@id", id);
            cmd.ExecuteNonQuery();
        }

        // -------- PAYMENT MODES (CRUD) --------
        public List<PaymentMode> GetPaymentModes()
        {
            var list = new List<PaymentMode>();
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"SELECT Id, Name, FeeFixed, FeeRate, IsActive
                        FROM PaymentModes
                        ORDER BY Name;";
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                list.Add(new PaymentMode
                {
                    Id = rd.GetInt32(0),
                    Name = rd.GetString(1),
                    FeeFixed = Convert.ToDecimal(rd.GetDouble(2)),
                    FeeRate = Convert.ToDecimal(rd.GetDouble(3)),
                    IsActive = rd.GetInt32(4) != 0
                });
            }
            return list;
        }

        public int InsertPaymentMode(PaymentMode m)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
INSERT INTO PaymentModes(Name, FeeFixed, FeeRate, IsActive)
VALUES(@n,@ff,@fr,@a);
SELECT last_insert_rowid();";
            Db.AddParam(cmd, "@n", m.Name ?? "");
            Db.AddParam(cmd, "@ff", m.FeeFixed);
            Db.AddParam(cmd, "@fr", m.FeeRate);
            Db.AddParam(cmd, "@a", m.IsActive ? 1 : 0);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public void UpdatePaymentMode(PaymentMode m)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
UPDATE PaymentModes SET
  Name=@n, FeeFixed=@ff, FeeRate=@fr, IsActive=@a
WHERE Id=@id;";
            Db.AddParam(cmd, "@n", m.Name ?? "");
            Db.AddParam(cmd, "@ff", m.FeeFixed);
            Db.AddParam(cmd, "@fr", m.FeeRate);
            Db.AddParam(cmd, "@a", m.IsActive ? 1 : 0);
            Db.AddParam(cmd, "@id", m.Id);
            cmd.ExecuteNonQuery();
        }

        public void DeletePaymentMode(int id)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "DELETE FROM PaymentModes WHERE Id=@id;";
            Db.AddParam(cmd, "@id", id);
            cmd.ExecuteNonQuery();
        }

        // -------- PaymentTerms CRUD --------
        public List<PaymentTerm> GetPaymentTerms()
        {
            var list = new List<PaymentTerm>();
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"SELECT Id, Name, Mode, SimpleDue, OrderPct, IsDefault, Body
                        FROM PaymentTerms
                        ORDER BY IsDefault DESC, Name COLLATE NOCASE;";
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                list.Add(new PaymentTerm
                {
                    Id = rd.GetInt32(0),
                    Name = rd.IsDBNull(1) ? "" : rd.GetString(1),
                    Mode = rd.IsDBNull(2) ? "SIMPLE" : rd.GetString(2),
                    SimpleDue = rd.IsDBNull(3) ? null : rd.GetString(3),
                    OrderPct = rd.IsDBNull(4) ? (double?)null : rd.GetDouble(4),
                    IsDefault = !rd.IsDBNull(5) && rd.GetInt32(5) != 0,
                    Body = rd.IsDBNull(6) ? "" : rd.GetString(6),
                });
            }
            return list;
        }

        public int InsertPaymentTerm(PaymentTerm t)
        {
            using var cn = Db.Open();
            using var tx = cn.BeginTransaction();

            if (t.IsDefault)
            {
                using var clear = cn.CreateCommand();
                clear.CommandText = "UPDATE PaymentTerms SET IsDefault=0;";
                clear.ExecuteNonQuery();
            }

            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
INSERT INTO PaymentTerms(Name, Mode, SimpleDue, OrderPct, IsDefault, Body)
VALUES(@n,@m,@sd,@pct,@d,@b);
SELECT last_insert_rowid();";
            Db.AddParam(cmd, "@n", t.Name ?? "");
            Db.AddParam(cmd, "@m", t.Mode ?? "SIMPLE");
            Db.AddParam(cmd, "@sd", (object?)t.SimpleDue ?? DBNull.Value);
            Db.AddParam(cmd, "@pct", (object?)t.OrderPct ?? DBNull.Value);
            Db.AddParam(cmd, "@d", t.IsDefault ? 1 : 0);
            Db.AddParam(cmd, "@b", t.Body ?? "");
            var id = Convert.ToInt32(cmd.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);

            tx.Commit();
            return id;
        }

        public void UpdatePaymentTerm(PaymentTerm t)
        {
            using var cn = Db.Open();
            using var tx = cn.BeginTransaction();

            if (t.IsDefault)
            {
                using var clear = cn.CreateCommand();
                clear.CommandText = "UPDATE PaymentTerms SET IsDefault=0;";
                clear.ExecuteNonQuery();
            }

            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
UPDATE PaymentTerms SET
  Name=@n, Mode=@m, SimpleDue=@sd, OrderPct=@pct, IsDefault=@d, Body=@b
WHERE Id=@id;";
            Db.AddParam(cmd, "@id", t.Id);
            Db.AddParam(cmd, "@n", t.Name ?? "");
            Db.AddParam(cmd, "@m", t.Mode ?? "SIMPLE");
            Db.AddParam(cmd, "@sd", (object?)t.SimpleDue ?? DBNull.Value);
            Db.AddParam(cmd, "@pct", (object?)t.OrderPct ?? DBNull.Value);
            Db.AddParam(cmd, "@d", t.IsDefault ? 1 : 0);
            Db.AddParam(cmd, "@b", t.Body ?? "");
            cmd.ExecuteNonQuery();

            tx.Commit();
        }

        public void DeletePaymentTerm(int id)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "DELETE FROM PaymentTerms WHERE Id=@id;";
            Db.AddParam(cmd, "@id", id);
            cmd.ExecuteNonQuery();
        }

        public PaymentTerm? GetPaymentTermById(int id)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"SELECT Id, Name, Mode, SimpleDue, OrderPct, IsDefault, Body
                        FROM PaymentTerms WHERE Id=@id;";
            Db.AddParam(cmd, "@id", id);
            using var rd = cmd.ExecuteReader();
            if (!rd.Read()) return null;
            return new PaymentTerm
            {
                Id = rd.GetInt32(0),
                Name = rd.IsDBNull(1) ? "" : rd.GetString(1),
                Mode = rd.IsDBNull(2) ? "SIMPLE" : rd.GetString(2),
                SimpleDue = rd.IsDBNull(3) ? null : rd.GetString(3),
                OrderPct = rd.IsDBNull(4) ? (double?)null : rd.GetDouble(4),
                IsDefault = !rd.IsDBNull(5) && rd.GetInt32(5) != 0,
                Body = rd.IsDBNull(6) ? "" : rd.GetString(6),
            };
        }

        // -------- ANNEXES (catalogue) --------
        public List<(int Id, string Nom, string CheminRelatif, bool Actif)> GetAnnexCatalog()
        {
            var list = new List<(int, string, string, bool)>();
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT Id, Nom, CheminRelatif, Actif FROM Annexes ORDER BY Nom COLLATE NOCASE;";
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
                list.Add((rd.GetInt32(0), rd.GetString(1), rd.GetString(2), rd.GetInt32(3) != 0));
            return list;
        }

        public int InsertAnnex(string nom, string cheminRelatif, bool actif = true)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
INSERT INTO Annexes(Nom, CheminRelatif, Actif) VALUES(@n,@p,@a);
SELECT last_insert_rowid();";
            Db.AddParam(cmd, "@n", nom ?? "");
            Db.AddParam(cmd, "@p", cheminRelatif ?? "");
            Db.AddParam(cmd, "@a", actif ? 1 : 0);
            return Convert.ToInt32(cmd.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
        }

        public void UpdateAnnex(int id, string nom)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "UPDATE Annexes SET Nom=@n WHERE Id=@id;";
            Db.AddParam(cmd, "@id", id);
            Db.AddParam(cmd, "@n", nom ?? "");
            cmd.ExecuteNonQuery();
        }

        public void DeleteAnnex(int id)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "DELETE FROM Annexes WHERE Id=@id;";
            Db.AddParam(cmd, "@id", id);
            cmd.ExecuteNonQuery();
        }
    }
}
