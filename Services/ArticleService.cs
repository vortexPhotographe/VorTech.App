using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using VorTech.App;
using VorTech.App.Models;

namespace VorTech.App.Services
{
    public class ArticleService
    {
        public ArticleService()
        {
            EnsureSchema();
        }

        private void EnsureSchema()
        {
            using var cn = Db.Open();

            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS Articles(
  Id               INTEGER PRIMARY KEY AUTOINCREMENT,
  Code             TEXT NOT NULL,
  Libelle          TEXT NOT NULL,
  Type             TEXT NULL,
  PrixAchatHT      REAL DEFAULT 0,
  PrixVenteHT      REAL DEFAULT 0,
  TVA              REAL DEFAULT 0,
  StockActuel      REAL DEFAULT 0,
  SeuilAlerte      REAL DEFAULT 0,
  Actif            INTEGER DEFAULT 1,
  DerniereMAJ      TEXT NULL,
  TauxCotisation   REAL DEFAULT 0,
  CotisationTypeId INTEGER NULL,
  TvaRateId        INTEGER NULL,
  Barcode          TEXT NULL,
  BarcodeType      TEXT NOT NULL DEFAULT 'CODE128',
  PoidsUnitaireGr  REAL DEFAULT 0
);";
                cmd.ExecuteNonQuery();
            }

            // Colonnes manquantes (idempotent)
            var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA table_info(Articles);";
                using var rd = cmd.ExecuteReader();
                while (rd.Read()) existing.Add(rd.GetString(1));
            }
            void AddCol(string name, string typeSql)
            {
                if (!existing.Contains(name))
                {
                    using var alter = cn.CreateCommand();
                    alter.CommandText = $"ALTER TABLE Articles ADD COLUMN {name} {typeSql};";
                    alter.ExecuteNonQuery();
                }
            }

            AddCol("Code",            "TEXT");
            AddCol("Libelle",         "TEXT");
            AddCol("Type",            "TEXT NULL");
            AddCol("PrixAchatHT",     "REAL DEFAULT 0");
            AddCol("PrixVenteHT",     "REAL DEFAULT 0");
            AddCol("TVA",             "REAL DEFAULT 0");
            AddCol("StockActuel",     "REAL DEFAULT 0");
            AddCol("SeuilAlerte",     "REAL DEFAULT 0");
            AddCol("Actif",           "INTEGER DEFAULT 1");
            AddCol("DerniereMAJ",     "TEXT NULL");
            AddCol("TauxCotisation",  "REAL DEFAULT 0");
            AddCol("CotisationTypeId","INTEGER NULL");
            AddCol("TvaRateId",       "INTEGER NULL");
            AddCol("Barcode",         "TEXT NULL");
            AddCol("BarcodeType",     "TEXT NOT NULL DEFAULT 'CODE128'");
            AddCol("PoidsUnitaireGr", "REAL DEFAULT 0");

            // Index utiles
            using (var idx = cn.CreateCommand())
            {
                idx.CommandText = @"CREATE INDEX IF NOT EXISTS IX_Articles_Libelle ON Articles(Libelle);";
                idx.ExecuteNonQuery();
            }
            try
            {
                using var u1 = cn.CreateCommand();
                u1.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS UX_Articles_Code ON Articles(Code);";
                u1.ExecuteNonQuery();
            }
            catch { /* doublons éventuels -> on n'empêche pas l'appli */ }
            try
            {
                using var u2 = cn.CreateCommand();
                u2.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS UX_Articles_Barcode ON Articles(Barcode);";
                u2.ExecuteNonQuery();
            }
            catch { /* si barcodes dupliqués historiques */ }

            // Historique réassorts
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS ArticleReassorts(
  Id          INTEGER PRIMARY KEY AUTOINCREMENT,
  ArticleId   INTEGER NOT NULL,
  Date        TEXT NOT NULL,
  Fournisseur TEXT NULL,
  Qte         REAL NOT NULL,
  PUAchatHT   REAL NOT NULL,
  Notes       TEXT NULL,
  FOREIGN KEY(ArticleId) REFERENCES Articles(Id)
);";
                cmd.ExecuteNonQuery();
            }

            // Table composition packs (inchangée si déjà créée ailleurs)
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS ArticleComponents(
  Id                 INTEGER PRIMARY KEY AUTOINCREMENT,
  PackArticleId      INTEGER NOT NULL,
  ComponentArticleId INTEGER NOT NULL,
  ComponentVariantId INTEGER NULL,
  Qte                REAL DEFAULT 1,
  FOREIGN KEY(PackArticleId) REFERENCES Articles(Id),
  FOREIGN KEY(ComponentArticleId) REFERENCES Articles(Id)
);";
                cmd.ExecuteNonQuery();
            }
        }

        // ===== CRUD Articles =================================================

        public List<Article> GetAll()
        {
            var list = new List<Article>();
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
SELECT Id, Code, Libelle, Type, PrixAchatHT, PrixVenteHT, TVA,
       StockActuel, SeuilAlerte, Actif, DerniereMAJ, TauxCotisation,
       CotisationTypeId, TvaRateId, Barcode, BarcodeType, PoidsUnitaireGr
FROM Articles
ORDER BY Libelle;";
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                var a = new Article
                {
                    Id = rd.GetInt32(0),
                    Code = rd.IsDBNull(1) ? "" : rd.GetString(1),
                    Libelle = rd.IsDBNull(2) ? "" : rd.GetString(2),
                    Type = rd.IsDBNull(3) ? null : rd.GetString(3),
                    PrixAchatHT = rd.IsDBNull(4) ? 0 : rd.GetDouble(4),
                    PrixVenteHT = rd.IsDBNull(5) ? 0 : rd.GetDouble(5),
                    TVA = rd.IsDBNull(6) ? 0 : rd.GetDouble(6),
                    StockActuel = rd.IsDBNull(7) ? 0 : rd.GetDouble(7),
                    SeuilAlerte = rd.IsDBNull(8) ? 0 : rd.GetDouble(8),
                    Actif = !rd.IsDBNull(9) && rd.GetInt32(9) != 0,
                    DerniereMAJ = rd.IsDBNull(10) ? null : rd.GetString(10),
                    TauxCotisation = rd.IsDBNull(11) ? 0 : rd.GetDouble(11),
                    CotisationTypeId = rd.IsDBNull(12) ? (int?)null : rd.GetInt32(12),
                    TvaRateId = rd.IsDBNull(13) ? (int?)null : rd.GetInt32(13),
                    Barcode = rd.IsDBNull(14) ? null : rd.GetString(14),
                    BarcodeType = rd.IsDBNull(15) ? "CODE128" : rd.GetString(15),
                    PoidsUnitaireGr = rd.IsDBNull(16) ? 0 : rd.GetDouble(16)
                };
                list.Add(a);
            }
            return list;
        }

        public int Add(Article a)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
INSERT INTO Articles
(Code, Libelle, Type, PrixAchatHT, PrixVenteHT, TVA,
 StockActuel, SeuilAlerte, Actif, DerniereMAJ, TauxCotisation,
 CotisationTypeId, TvaRateId, Barcode, BarcodeType, PoidsUnitaireGr)
VALUES
($code, $lib, $type, $pa, $pv, $tva,
 $stock, $seuil, $actif, $maj, $cot,
 $ctid, $tvaid, $bar, $bart, $poids);
SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("$code", a.Code ?? "");
            cmd.Parameters.AddWithValue("$lib", a.Libelle ?? "");
            cmd.Parameters.AddWithValue("$type", (object?)a.Type ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$pa", a.PrixAchatHT);
            cmd.Parameters.AddWithValue("$pv", a.PrixVenteHT);
            cmd.Parameters.AddWithValue("$tva", a.TVA);
            cmd.Parameters.AddWithValue("$stock", a.StockActuel);
            cmd.Parameters.AddWithValue("$seuil", a.SeuilAlerte);
            cmd.Parameters.AddWithValue("$actif", a.Actif ? 1 : 0);
            cmd.Parameters.AddWithValue("$maj", (object?)a.DerniereMAJ ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$cot", a.TauxCotisation);
            cmd.Parameters.AddWithValue("$ctid", (object?)a.CotisationTypeId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$tvaid", (object?)a.TvaRateId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$bar", (object?)a.Barcode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$bart", a.BarcodeType ?? "CODE128");
            cmd.Parameters.AddWithValue("$poids", a.PoidsUnitaireGr);
            var scalar = cmd.ExecuteScalar();
            long id = scalar is long l ? l : Convert.ToInt64(scalar ?? 0);
            return (int)id;
        }

        public void Update(Article a)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
UPDATE Articles
SET Code=$code, Libelle=$lib, Type=$type, PrixAchatHT=$pa, PrixVenteHT=$pv, TVA=$tva,
    StockActuel=$stock, SeuilAlerte=$seuil, Actif=$actif, DerniereMAJ=$maj, TauxCotisation=$cot,
    CotisationTypeId=$ctid, TvaRateId=$tvaid, Barcode=$bar, BarcodeType=$bart, PoidsUnitaireGr=$poids
WHERE Id=$id;";
            cmd.Parameters.AddWithValue("$id", a.Id);
            cmd.Parameters.AddWithValue("$code", a.Code ?? "");
            cmd.Parameters.AddWithValue("$lib", a.Libelle ?? "");
            cmd.Parameters.AddWithValue("$type", (object?)a.Type ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$pa", a.PrixAchatHT);
            cmd.Parameters.AddWithValue("$pv", a.PrixVenteHT);
            cmd.Parameters.AddWithValue("$tva", a.TVA);
            cmd.Parameters.AddWithValue("$stock", a.StockActuel);
            cmd.Parameters.AddWithValue("$seuil", a.SeuilAlerte);
            cmd.Parameters.AddWithValue("$actif", a.Actif ? 1 : 0);
            cmd.Parameters.AddWithValue("$maj", (object?)a.DerniereMAJ ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$cot", a.TauxCotisation);
            cmd.Parameters.AddWithValue("$ctid", (object?)a.CotisationTypeId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$tvaid", (object?)a.TvaRateId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$bar", (object?)a.Barcode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$bart", a.BarcodeType ?? "CODE128");
            cmd.Parameters.AddWithValue("$poids", a.PoidsUnitaireGr);
            cmd.ExecuteNonQuery();
        }

        public void Save(Article a)
        {
            if (a == null) return;
            if (a.Id == 0) a.Id = Add(a); else Update(a);
        }

        public void Delete(int id)
        {
            using var cn = Db.Open();
            using var tx = cn.BeginTransaction();

            using (var cmd = cn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "DELETE FROM ArticleComponents WHERE PackArticleId=$id OR ComponentArticleId=$id;";
                cmd.Parameters.AddWithValue("$id", id);
                cmd.ExecuteNonQuery();
            }
            using (var cmd = cn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "DELETE FROM Articles WHERE Id=$id;";
                cmd.Parameters.AddWithValue("$id", id);
                cmd.ExecuteNonQuery();
            }

            tx.Commit();
        }

        // ===== Réassorts (PMP) ==============================================

        public List<ArticleReassort> GetReassorts(int articleId)
        {
            var list = new List<ArticleReassort>();
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
SELECT Id, ArticleId, Date, Fournisseur, Qte, PUAchatHT, Notes
FROM ArticleReassorts
WHERE ArticleId=$a
ORDER BY Date DESC, Id DESC;";
            cmd.Parameters.AddWithValue("$a", articleId);
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                list.Add(new ArticleReassort
                {
                    Id = rd.GetInt32(0),
                    ArticleId = rd.GetInt32(1),
                    Date = rd.IsDBNull(2) ? "" : rd.GetString(2),
                    Fournisseur = rd.IsDBNull(3) ? null : rd.GetString(3),
                    Qte = rd.IsDBNull(4) ? 0 : rd.GetDouble(4),
                    PUAchatHT = rd.IsDBNull(5) ? 0 : rd.GetDouble(5),
                    Notes = rd.IsDBNull(6) ? null : rd.GetString(6)
                });
            }
            return list;
        }

        /// <summary>
        /// Ajoute un réassort et met à jour le StockActuel et le PrixAchatHT selon PMP.
        /// </summary>
        public void AddReassortPmp(int articleId, double qte, double puAchatHt, string? fournisseur, string? notes)
        {
            if (qte <= 0 || puAchatHt < 0) return;

            using var cn = Db.Open();
            using var tx = cn.BeginTransaction();

            double stockActuel = 0;
            double prixAchatActuel = 0;

            using (var sel = cn.CreateCommand())
            {
                sel.Transaction = tx;
                sel.CommandText = "SELECT StockActuel, PrixAchatHT FROM Articles WHERE Id=$id;";
                sel.Parameters.AddWithValue("$id", articleId);
                using var rd = sel.ExecuteReader();
                if (rd.Read())
                {
                    stockActuel = rd.IsDBNull(0) ? 0 : rd.GetDouble(0);
                    prixAchatActuel = rd.IsDBNull(1) ? 0 : rd.GetDouble(1);
                }
            }

            // Nouveau PMP
            double nouveauPrixAchat = (stockActuel * prixAchatActuel + qte * puAchatHt) / (stockActuel + qte);

            // Ecrit réassort
            using (var ins = cn.CreateCommand())
            {
                ins.Transaction = tx;
                ins.CommandText = @"
INSERT INTO ArticleReassorts(ArticleId, Date, Fournisseur, Qte, PUAchatHT, Notes)
VALUES($a, $d, $f, $q, $p, $n);";
                ins.Parameters.AddWithValue("$a", articleId);
                ins.Parameters.AddWithValue("$d", DateTime.UtcNow.ToString("s"));
                ins.Parameters.AddWithValue("$f", (object?)fournisseur ?? DBNull.Value);
                ins.Parameters.AddWithValue("$q", qte);
                ins.Parameters.AddWithValue("$p", puAchatHt);
                ins.Parameters.AddWithValue("$n", (object?)notes ?? DBNull.Value);
                ins.ExecuteNonQuery();
            }

            // Mise à jour article
            using (var upd = cn.CreateCommand())
            {
                upd.Transaction = tx;
                upd.CommandText = @"
UPDATE Articles
SET StockActuel = StockActuel + $q,
    PrixAchatHT = $p,
    DerniereMAJ = $now
WHERE Id=$id;";
                upd.Parameters.AddWithValue("$q", qte);
                upd.Parameters.AddWithValue("$p", nouveauPrixAchat);
                upd.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("s"));
                upd.Parameters.AddWithValue("$id", articleId);
                upd.ExecuteNonQuery();
            }

            tx.Commit();
        }
    }
}
