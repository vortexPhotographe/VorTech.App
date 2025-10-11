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

            // 1) Table Articles (création "propre" sans contrainte UNIQUE inline)
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS Articles(
  Id             INTEGER PRIMARY KEY AUTOINCREMENT,
  Code           TEXT NOT NULL,          -- l'unicité sera assurée par un index dédié
  Libelle        TEXT NOT NULL,
  Type           TEXT NULL,              -- 'Produit', 'Service', 'Pack'...
  PrixAchatHT    REAL DEFAULT 0,
  PrixVenteHT    REAL DEFAULT 0,
  TVA            REAL DEFAULT 0,
  StockActuel    REAL DEFAULT 0,
  SeuilAlerte    REAL DEFAULT 0,
  Actif          INTEGER DEFAULT 1,
  DerniereMAJ    TEXT NULL,              -- ISO 8601
  TauxCotisation REAL DEFAULT 0
);";
                cmd.ExecuteNonQuery();
            }

            // 2) Ajouter colonnes manquantes (idempotent) — jamais de UNIQUE dans ALTER TABLE
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
            AddCol("Code",           "TEXT");                 // pas de UNIQUE ici
            AddCol("Libelle",        "TEXT");
            AddCol("Type",           "TEXT NULL");
            AddCol("PrixAchatHT",    "REAL DEFAULT 0");
            AddCol("PrixVenteHT",    "REAL DEFAULT 0");
            AddCol("TVA",            "REAL DEFAULT 0");
            AddCol("StockActuel",    "REAL DEFAULT 0");
            AddCol("SeuilAlerte",    "REAL DEFAULT 0");
            AddCol("Actif",          "INTEGER DEFAULT 1");
            AddCol("DerniereMAJ",    "TEXT NULL");
            AddCol("TauxCotisation", "REAL DEFAULT 0");

            // 3) Index (non-unique) utiles
            using (var idx = cn.CreateCommand())
            {
                idx.CommandText = @"
CREATE INDEX IF NOT EXISTS IX_Articles_Libelle ON Articles(Libelle);";
                idx.ExecuteNonQuery();
            }

            // 4) Index UNIQUE sur Code (créé séparément)
            //    - OK si base neuve
            //    - Si doublons hérités -> CREATE UNIQUE INDEX échouera ; on ignore pour ne pas faire planter l'app,
            //      et on laissera l'utilisateur corriger les doublons depuis l'UI plus tard.
            try
            {
                using var uidx = cn.CreateCommand();
                uidx.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS UX_Articles_Code ON Articles(Code);";
                uidx.ExecuteNonQuery();
            }
            catch
            {
                // Laisse passer (probable doublon existant).
                // TODO (optionnel): log interne ou notification douce dans l'UI.
            }

            // 5) Table de composition des packs
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS ArticleComponents(
  Id                 INTEGER PRIMARY KEY AUTOINCREMENT,
  PackArticleId      INTEGER NOT NULL,
  ComponentArticleId INTEGER NOT NULL,
  Qte                REAL DEFAULT 1,
  FOREIGN KEY(PackArticleId) REFERENCES Articles(Id),
  FOREIGN KEY(ComponentArticleId) REFERENCES Articles(Id)
);";
                cmd.ExecuteNonQuery();
            }
        }

        // ===== CRUD =====

        public List<Article> GetAll()
        {
            var list = new List<Article>();
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
SELECT Id, Code, Libelle, Type, PrixAchatHT, PrixVenteHT, TVA,
       StockActuel, SeuilAlerte, Actif, DerniereMAJ, TauxCotisation
FROM Articles
ORDER BY Libelle;";
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                var a = new Article
                {
                    Id             = rd.GetInt32(0),
                    Code           = rd.IsDBNull(1) ? "" : rd.GetString(1),
                    Libelle        = rd.IsDBNull(2) ? "" : rd.GetString(2),
                    Type           = rd.IsDBNull(3) ? null : rd.GetString(3),
                    PrixAchatHT    = rd.IsDBNull(4) ? 0 : rd.GetDouble(4),
                    PrixVenteHT    = rd.IsDBNull(5) ? 0 : rd.GetDouble(5),
                    TVA            = rd.IsDBNull(6) ? 0 : rd.GetDouble(6),
                    StockActuel    = rd.IsDBNull(7) ? 0 : rd.GetDouble(7),
                    SeuilAlerte    = rd.IsDBNull(8) ? 0 : rd.GetDouble(8),
                    Actif          = !rd.IsDBNull(9) && rd.GetInt32(9) != 0,
                    DerniereMAJ    = rd.IsDBNull(10) ? null : rd.GetString(10),
                    TauxCotisation = rd.IsDBNull(11) ? 0 : rd.GetDouble(11)
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
 StockActuel, SeuilAlerte, Actif, DerniereMAJ, TauxCotisation)
VALUES
($code, $lib, $type, $pa, $pv, $tva,
 $stock, $seuil, $actif, $maj, $cot);
SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("$code",  a.Code ?? "");
            cmd.Parameters.AddWithValue("$lib",   a.Libelle ?? "");
            cmd.Parameters.AddWithValue("$type",  (object?)a.Type ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$pa",    a.PrixAchatHT);
            cmd.Parameters.AddWithValue("$pv",    a.PrixVenteHT);
            cmd.Parameters.AddWithValue("$tva",   a.TVA);
            cmd.Parameters.AddWithValue("$stock", a.StockActuel);
            cmd.Parameters.AddWithValue("$seuil", a.SeuilAlerte);
            cmd.Parameters.AddWithValue("$actif", a.Actif ? 1 : 0);
            cmd.Parameters.AddWithValue("$maj",   (object?)a.DerniereMAJ ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$cot",   a.TauxCotisation);

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
    StockActuel=$stock, SeuilAlerte=$seuil, Actif=$actif, DerniereMAJ=$maj, TauxCotisation=$cot
WHERE Id=$id;";
            cmd.Parameters.AddWithValue("$id",    a.Id);
            cmd.Parameters.AddWithValue("$code",  a.Code ?? "");
            cmd.Parameters.AddWithValue("$lib",   a.Libelle ?? "");
            cmd.Parameters.AddWithValue("$type",  (object?)a.Type ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$pa",    a.PrixAchatHT);
            cmd.Parameters.AddWithValue("$pv",    a.PrixVenteHT);
            cmd.Parameters.AddWithValue("$tva",   a.TVA);
            cmd.Parameters.AddWithValue("$stock", a.StockActuel);
            cmd.Parameters.AddWithValue("$seuil", a.SeuilAlerte);
            cmd.Parameters.AddWithValue("$actif", a.Actif ? 1 : 0);
            cmd.Parameters.AddWithValue("$maj",   (object?)a.DerniereMAJ ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$cot",   a.TauxCotisation);
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

        // ===== Packs (composition) =====

        public List<ArticleComponent> GetComponents(int packArticleId)
        {
            var list = new List<ArticleComponent>();
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
SELECT ac.Id, ac.PackArticleId, ac.ComponentArticleId, ac.Qte,
       a.Code, a.Libelle
FROM ArticleComponents ac
LEFT JOIN Articles a ON a.Id = ac.ComponentArticleId
WHERE ac.PackArticleId = $id
ORDER BY a.Libelle;";
            cmd.Parameters.AddWithValue("$id", packArticleId);
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                list.Add(new ArticleComponent
                {
                    Id = rd.GetInt32(0),
                    PackArticleId = rd.GetInt32(1),
                    ComponentArticleId = rd.GetInt32(2),
                    Qte = rd.IsDBNull(3) ? 1 : rd.GetDouble(3),
                    ComponentCode = rd.IsDBNull(4) ? null : rd.GetString(4),
                    ComponentLibelle = rd.IsDBNull(5) ? null : rd.GetString(5)
                });
            }
            return list;
        }

        public void SetComponents(int packArticleId, IEnumerable<ArticleComponent> items)
        {
            using var cn = Db.Open();
            using var tx = cn.BeginTransaction();

            using (var del = cn.CreateCommand())
            {
                del.Transaction = tx;
                del.CommandText = "DELETE FROM ArticleComponents WHERE PackArticleId=$id;";
                del.Parameters.AddWithValue("$id", packArticleId);
                del.ExecuteNonQuery();
            }

            foreach (var it in items)
            {
                using var ins = cn.CreateCommand();
                ins.Transaction = tx;
                ins.CommandText = @"
INSERT INTO ArticleComponents(PackArticleId, ComponentArticleId, Qte)
VALUES($pid, $cid, $qte);";
                ins.Parameters.AddWithValue("$pid", packArticleId);
                ins.Parameters.AddWithValue("$cid", it.ComponentArticleId);
                ins.Parameters.AddWithValue("$qte", it.Qte);
                ins.ExecuteNonQuery();
            }

            tx.Commit();
        }

        public int? ResolveArticleIdByCode(string code)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT Id FROM Articles WHERE Code=$c LIMIT 1;";
            cmd.Parameters.AddWithValue("$c", code ?? "");
            var scalar = cmd.ExecuteScalar();
            if (scalar == null || scalar is DBNull) return null;
            return Convert.ToInt32(scalar);
        }
    }
}
