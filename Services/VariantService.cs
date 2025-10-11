using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;
using VorTech.App.Models;

namespace VorTech.App.Services
{
    /// <summary>
    /// Déclinaisons multi-axes (axes/valeurs) + Variantes d'articles.
    /// Idempotent, compatible SQLite.
    /// </summary>
    public class VariantService
    {
        public VariantService()
        {
            EnsureSchema();
        }

        private void EnsureSchema()
        {
            using var cn = Db.Open();

            // Axes (VariantOptions)
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS VariantOptions(
  Id    INTEGER PRIMARY KEY AUTOINCREMENT,
  Name  TEXT NOT NULL,
  Notes TEXT NULL
);";
                cmd.ExecuteNonQuery();
            }

            // Valeurs par axe
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS VariantOptionValues(
  Id       INTEGER PRIMARY KEY AUTOINCREMENT,
  OptionId INTEGER NOT NULL,
  Value    TEXT NOT NULL,
  FOREIGN KEY(OptionId) REFERENCES VariantOptions(Id)
);";
                cmd.ExecuteNonQuery();
            }

            // Variantes d'un article
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS ArticleVariants(
  Id              INTEGER PRIMARY KEY AUTOINCREMENT,
  ArticleId       INTEGER NOT NULL,
  Code            TEXT NOT NULL,         -- UNIQUE via index dédié
  Barcode         TEXT NULL,
  BarcodeType     TEXT NOT NULL DEFAULT 'CODE128',
  PrixAchatHT     REAL NULL,
  PrixVenteHT     REAL NULL,
  StockActuel     REAL DEFAULT 0,
  PoidsUnitaireGr REAL DEFAULT 0,
  Actif           INTEGER DEFAULT 1,
  FOREIGN KEY(ArticleId) REFERENCES Articles(Id)
);";
                cmd.ExecuteNonQuery();
            }

            // Unicité sur Code
            try
            {
                using var idx = cn.CreateCommand();
                idx.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS UX_ArticleVariants_Code ON ArticleVariants(Code);";
                idx.ExecuteNonQuery();
            }
            catch { /* doublons historiques -> on laisse démarrer l'app */ }

            // Sélections axe->valeur pour chaque variante
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS ArticleVariantSelections(
  VariantId INTEGER NOT NULL,
  OptionId  INTEGER NOT NULL,
  ValueId   INTEGER NOT NULL,
  PRIMARY KEY (VariantId, OptionId),
  FOREIGN KEY(VariantId) REFERENCES ArticleVariants(Id),
  FOREIGN KEY(OptionId)  REFERENCES VariantOptions(Id),
  FOREIGN KEY(ValueId)   REFERENCES VariantOptionValues(Id)
);";
                cmd.ExecuteNonQuery();
            }

            // Evolution pack: possibilité de pointer une variante précise en composant (nullable)
            var existingCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var c = cn.CreateCommand())
            {
                c.CommandText = "PRAGMA table_info(ArticleComponents);";
                using var rd = c.ExecuteReader();
                while (rd.Read()) existingCols.Add(rd.GetString(1));
            }
            if (existingCols.Count > 0 && !existingCols.Contains("ComponentVariantId"))
            {
                using var alter = cn.CreateCommand();
                alter.CommandText = "ALTER TABLE ArticleComponents ADD COLUMN ComponentVariantId INTEGER NULL;";
                alter.ExecuteNonQuery();
            }
        }

        // ---- Axes & valeurs -------------------------------------------------

        public List<VariantOption> GetOptions()
        {
            var list = new List<VariantOption>();
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, Notes FROM VariantOptions ORDER BY Name;";
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                list.Add(new VariantOption
                {
                    Id = rd.GetInt32(0),
                    Name = rd.IsDBNull(1) ? "" : rd.GetString(1),
                    Notes = rd.IsDBNull(2) ? null : rd.GetString(2)
                });
            }
            return list;
        }

        public List<VariantOptionValue> GetOptionValues(int optionId)
        {
            var list = new List<VariantOptionValue>();
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT Id, OptionId, Value FROM VariantOptionValues WHERE OptionId=$o ORDER BY Value;";
            cmd.Parameters.AddWithValue("$o", optionId);
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                list.Add(new VariantOptionValue
                {
                    Id = rd.GetInt32(0),
                    OptionId = rd.GetInt32(1),
                    Value = rd.IsDBNull(2) ? "" : rd.GetString(2)
                });
            }
            return list;
        }

        // ---- Variantes ------------------------------------------------------

        public List<ArticleVariant> GetVariants(int articleId)
        {
            var list = new List<ArticleVariant>();
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
SELECT Id, ArticleId, Code, Barcode, BarcodeType,
       PrixAchatHT, PrixVenteHT, StockActuel, PoidsUnitaireGr, Actif
FROM ArticleVariants
WHERE ArticleId=$id
ORDER BY Code;";
            cmd.Parameters.AddWithValue("$id", articleId);
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                list.Add(new ArticleVariant
                {
                    Id = rd.GetInt32(0),
                    ArticleId = rd.GetInt32(1),
                    Code = rd.IsDBNull(2) ? "" : rd.GetString(2),
                    Barcode = rd.IsDBNull(3) ? null : rd.GetString(3),
                    BarcodeType = rd.IsDBNull(4) ? "CODE128" : rd.GetString(4),
                    PrixAchatHT = rd.IsDBNull(5) ? (double?)null : rd.GetDouble(5),
                    PrixVenteHT = rd.IsDBNull(6) ? (double?)null : rd.GetDouble(6),
                    StockActuel = rd.IsDBNull(7) ? 0 : rd.GetDouble(7),
                    PoidsUnitaireGr = rd.IsDBNull(8) ? 0 : rd.GetDouble(8),
                    Actif = !rd.IsDBNull(9) && rd.GetInt32(9) != 0
                });
            }
            return list;
        }

        public int AddVariant(ArticleVariant v)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
INSERT INTO ArticleVariants
(ArticleId, Code, Barcode, BarcodeType, PrixAchatHT, PrixVenteHT, StockActuel, PoidsUnitaireGr, Actif)
VALUES
($a,$c,$b,$bt,$pa,$pv,$s,$p,$act);
SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("$a", v.ArticleId);
            cmd.Parameters.AddWithValue("$c", v.Code ?? "");
            cmd.Parameters.AddWithValue("$b", (object?)v.Barcode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$bt", v.BarcodeType ?? "CODE128");
            cmd.Parameters.AddWithValue("$pa", (object?)v.PrixAchatHT ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$pv", (object?)v.PrixVenteHT ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$s", v.StockActuel);
            cmd.Parameters.AddWithValue("$p", v.PoidsUnitaireGr);
            cmd.Parameters.AddWithValue("$act", v.Actif ? 1 : 0);
            var scalar = cmd.ExecuteScalar();
            long id = scalar is long l ? l : Convert.ToInt64(scalar ?? 0);
            return (int)id;
        }

        public void UpdateVariant(ArticleVariant v)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
UPDATE ArticleVariants SET
  Code=$c, Barcode=$b, BarcodeType=$bt, PrixAchatHT=$pa, PrixVenteHT=$pv,
  StockActuel=$s, PoidsUnitaireGr=$p, Actif=$act
WHERE Id=$id;";
            cmd.Parameters.AddWithValue("$id", v.Id);
            cmd.Parameters.AddWithValue("$c", v.Code ?? "");
            cmd.Parameters.AddWithValue("$b", (object?)v.Barcode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$bt", v.BarcodeType ?? "CODE128");
            cmd.Parameters.AddWithValue("$pa", (object?)v.PrixAchatHT ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$pv", (object?)v.PrixVenteHT ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$s", v.StockActuel);
            cmd.Parameters.AddWithValue("$p", v.PoidsUnitaireGr);
            cmd.Parameters.AddWithValue("$act", v.Actif ? 1 : 0);
            cmd.ExecuteNonQuery();
        }

        public void UpsertVariantSelections(int variantId, IEnumerable<ArticleVariantSelection> sels)
        {
            using var cn = Db.Open();
            using var tx = cn.BeginTransaction();

            using (var del = cn.CreateCommand())
            {
                del.Transaction = tx;
                del.CommandText = "DELETE FROM ArticleVariantSelections WHERE VariantId=$v;";
                del.Parameters.AddWithValue("$v", variantId);
                del.ExecuteNonQuery();
            }

            foreach (var s in sels)
            {
                using var ins = cn.CreateCommand();
                ins.Transaction = tx;
                ins.CommandText = @"
INSERT INTO ArticleVariantSelections(VariantId, OptionId, ValueId)
VALUES($v,$o,$val);";
                ins.Parameters.AddWithValue("$v", variantId);
                ins.Parameters.AddWithValue("$o", s.OptionId);
                ins.Parameters.AddWithValue("$val", s.ValueId);
                ins.ExecuteNonQuery();
            }

            tx.Commit();
        }

        // ---- Barcodes -------------------------------------------------------

        public static string GenerateBarcode(bool isVariant)
        {
            // CODE128 compatible: alphanum simple
            // AYYYYMMDD-xxxxx / VYYYYMMDD-xxxxx
            var prefix = isVariant ? "V" : "A";
            var date = DateTime.UtcNow.ToString("yyyyMMdd");
            var rnd = Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();
            return $"{prefix}{date}-{rnd}";
        }

        // ---- Générateur de combinaisons (garde-fou) -------------------------

        /// <summary>
        /// Génère les variantes manquantes à partir d’axes/valeurs (IDs).
        /// </summary>
        public int GenerateMissingVariants(
            int articleId,
            Dictionary<int, List<int>> optionToValues,
            int maxCombinaisons,
            Func<IReadOnlyDictionary<int, int>, string>? skuFactory = null)
        {
            var axes = optionToValues.Keys.ToList();
            if (axes.Count == 0) return 0;

            var combos = new List<Dictionary<int,int>>();

            void Recur(int idx, Dictionary<int,int> acc)
            {
                if (idx >= axes.Count)
                {
                    combos.Add(new Dictionary<int,int>(acc));
                    return;
                }
                var opt = axes[idx];
                if (!optionToValues.TryGetValue(opt, out var vals) || vals.Count == 0)
                    return;
                foreach (var v in vals)
                {
                    acc[opt] = v;
                    Recur(idx + 1, acc);
                }
                acc.Remove(opt);
            }

            Recur(0, new Dictionary<int,int>());

            if (combos.Count > maxCombinaisons)
                throw new InvalidOperationException($"Trop de combinaisons ({combos.Count}). Limite: {maxCombinaisons}.");

            // Variantes existantes (Codes) pour éviter doublons
            var existingCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var cn = Db.Open())
            using (var c = cn.CreateCommand())
            {
                c.CommandText = "SELECT Code FROM ArticleVariants WHERE ArticleId=$a;";
                c.Parameters.AddWithValue("$a", articleId);
                using var rd = c.ExecuteReader();
                while (rd.Read()) if (!rd.IsDBNull(0)) existingCodes.Add(rd.GetString(0));
            }

            int created = 0;

            foreach (var sel in combos)
            {
                string code = skuFactory != null
                    ? skuFactory(sel)
                    : $"ART{articleId}-" + string.Join("-", sel.Select(kv => $"O{kv.Key}V{kv.Value}"));

                if (existingCodes.Contains(code)) continue;

                var variant = new ArticleVariant
                {
                    ArticleId = articleId,
                    Code = code,
                    Barcode = GenerateBarcode(isVariant: true),
                    BarcodeType = "CODE128",
                    StockActuel = 0,
                    PoidsUnitaireGr = 0,
                    Actif = true
                };

                var variantId = AddVariant(variant);
                var sels = sel.Select(kv => new ArticleVariantSelection
                {
                    VariantId = variantId,
                    OptionId = kv.Key,
                    ValueId = kv.Value
                });

                UpsertVariantSelections(variantId, sels);
                created++;
            }

            return created;
        }
    }
}
