using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text.Json;
using VorTech.App.Models;

namespace VorTech.App.Services
{
    public interface IDevisService
    {
        int CreateDraft(int? clientId, Client? snapshot = null);
        void SetNotes(int id, string? haut, string? bas);
        void SetGlobalDiscount(int id, decimal remise);
        int AddLine(int devisId, string designation, decimal qty, decimal pu, int? articleId = null, int? varianteId = null, string? imagePath = null);
        void UpdateLine(int ligneId, string designation, decimal qty, decimal pu);
        void DeleteLine(int ligneId);
        void RecalcTotals(int devisId);
        void AttachAnnexPdf(int devisId, string relPath, int ordre);
        void AttachAnnexPlanche(int devisId, int ordre, object? config = null);
        void ReorderAnnexe(int annexeId, int newOrdre);
        string Emit(int devisId, INumberingService numbering);
        void MarkTransformed(int devisId);
        void SetClientSnapshot(int devisId, int? clientId, Client? c);
        void UpdateClientFields(int devisId, string? societe, string? nomPrenom,
                                string? adresse, string? cp, string? ville,
                                string? email, string? telephone);
        void SoftDelete(int id);
        List<Devis> GetAll(string? search = null);
        List<DevisLigne> GetLines(int devisId);
        List<DevisAnnexe> GetAnnexes(int devisId);
        Devis? GetById(int id);
    }

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
  DeletedAt TEXT NULL
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
CREATE INDEX IF NOT EXISTS IX_DevisAnnexes_Devis ON DevisAnnexes(DevisId);";
            cmd.ExecuteNonQuery();
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
        public string Emit(int devisId, INumberingService numbering)
        {
            using var cn = Db.Open();

            // date du devis
            DateOnly date;
            using (var get = cn.CreateCommand())
            {
                get.CommandText = "SELECT Date FROM Devis WHERE Id=@d;";
                Db.AddParam(get, "@d", devisId);
                var iso = Convert.ToString(get.ExecuteScalar()) ?? DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd");
                date = DateOnly.ParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            }

            // numéro
            var numero = numbering.Next("DEVI", date);

            // pose
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = "UPDATE Devis SET Numero=@n, Etat='Envoye' WHERE Id=@d;";
                Db.AddParam(cmd, "@n", numero);
                Db.AddParam(cmd, "@d", devisId);
                cmd.ExecuteNonQuery();
            }

            // emplacement PDF final (génération faite dans le module PDF à venir)
            var docsDir = Path.Combine(Paths.DataDir, "Docs", "Devis"); // portable ??
            Directory.CreateDirectory(docsDir);
            var final = Path.Combine(docsDir, $"{numero}.pdf");
            if (!File.Exists(final)) File.WriteAllBytes(final, Array.Empty<byte>()); // placeholder vide

            return final;
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

        // ----- mappers -----
        private static Devis MapDevis(IDataRecord r) => new Devis
        {
            Id = Convert.ToInt32(r["Id"]),
            Numero = r["Numero"]?.ToString(),
            Date = DateOnly.ParseExact(r["Date"]?.ToString() ?? DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd"), "yyyy-MM-dd", CultureInfo.InvariantCulture),
            Etat = r["Etat"]?.ToString() ?? "Brouillon",

            ClientId = r["ClientId"] == DBNull.Value ? null : Convert.ToInt32(r["ClientId"]),

            //  AJOUTE CES 2 LIGNES
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
    }
}
