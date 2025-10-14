using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using VorTech.App;            // Db, Logger, Paths
using VorTech.App.Models;

namespace VorTech.App.Services
{
    public class ArticleService
    {
        public ArticleService()
        {
            EnsureSchema();
        }

        private static void EnsureSchema()
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();

            // ---------- Articles ----------
            cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS Articles (
    Id                INTEGER PRIMARY KEY AUTOINCREMENT,
    Code              TEXT NOT NULL,
    Libelle           TEXT NOT NULL,
    Type              INTEGER NOT NULL,
    CategorieId       INTEGER NULL,
    TvaRateId         INTEGER NULL,
    CotisationRateId  INTEGER NULL,
    PrixAchatHT       REAL NOT NULL DEFAULT 0,
    PrixVenteHT       REAL NOT NULL DEFAULT 0,
    StockActuel       REAL NOT NULL DEFAULT 0,
    SeuilAlerte       REAL NOT NULL DEFAULT 0,
    PoidsG            REAL NOT NULL DEFAULT 0,
    Actif             INTEGER NOT NULL DEFAULT 1,
    CodeBarres        TEXT NULL,
    DerniereMaj       TEXT NOT NULL
);";
            cmd.ExecuteNonQuery();

            // ---------- Variantes ----------
            cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS ArticleVariants (
    Id           INTEGER PRIMARY KEY AUTOINCREMENT,
    ArticleId    INTEGER NOT NULL,
    Nom          TEXT NOT NULL,
    PrixAchatHT  REAL NOT NULL DEFAULT 0,
    PrixVenteHT  REAL NOT NULL DEFAULT 0,
    StockActuel  REAL NOT NULL DEFAULT 0,
    SeuilAlerte  REAL NOT NULL DEFAULT 0,
    CodeBarres   TEXT NULL,
    FOREIGN KEY (ArticleId) REFERENCES Articles(Id) ON DELETE CASCADE
);";
            cmd.ExecuteNonQuery();
            try
            {
                cmd.CommandText = "ALTER TABLE ArticleVariants ADD COLUMN PrixAchatHT REAL NOT NULL DEFAULT 0;";
                cmd.ExecuteNonQuery();
            }
            catch { /* colonne déjà existante */ }

            try
            {
                cmd.CommandText = "ALTER TABLE ArticleVariants ADD COLUMN StockActuel REAL NOT NULL DEFAULT 0;";
                cmd.ExecuteNonQuery();
            }
            catch { }

            try
            {
                cmd.CommandText = "ALTER TABLE ArticleVariants ADD COLUMN SeuilAlerte REAL NOT NULL DEFAULT 0;";
                cmd.ExecuteNonQuery();
            }
            catch { }

            // ---------- Packs ----------
            cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS PackItems (
    Id             INTEGER PRIMARY KEY AUTOINCREMENT,
    PackArticleId  INTEGER NOT NULL,
    ArticleItemId  INTEGER NOT NULL,
    Quantite       REAL NOT NULL DEFAULT 1,
    FOREIGN KEY (PackArticleId) REFERENCES Articles(Id) ON DELETE CASCADE,
    FOREIGN KEY (ArticleItemId) REFERENCES Articles(Id) ON DELETE RESTRICT
);";
            cmd.ExecuteNonQuery();

            // ---------- Images ----------
            cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS ArticleImages (
    Id         INTEGER PRIMARY KEY AUTOINCREMENT,
    ArticleId  INTEGER NOT NULL,
    Slot       INTEGER NOT NULL,      -- 1..4
    RelPath    TEXT NOT NULL,
    UNIQUE(ArticleId, Slot),
    FOREIGN KEY (ArticleId) REFERENCES Articles(Id) ON DELETE CASCADE
);";
            cmd.ExecuteNonQuery();
        }

        // ======================================================
        //                       ARTICLES
        // ======================================================

        public IEnumerable<Article> GetAll()
        {
            var list = new List<Article>();

            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
SELECT Id, Code, Libelle, Type, CategorieId, TvaRateId, CotisationRateId,
       PrixAchatHT, PrixVenteHT, StockActuel, SeuilAlerte, PoidsG, Actif, CodeBarres, DerniereMaj
FROM Articles
ORDER BY Id DESC;";

            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                int i = 0;

                int id = rd.GetInt32(i++);

                string code;
                if (rd.IsDBNull(i)) { code = string.Empty; i++; }
                else code = rd.GetString(i++);

                string libelle;
                if (rd.IsDBNull(i)) { libelle = string.Empty; i++; }
                else libelle = rd.GetString(i++);

                var type = (ArticleType)rd.GetInt32(i++);

                int? catId = rd.IsDBNull(i) ? null : rd.GetInt32(i); i++;
                int? tvaId = rd.IsDBNull(i) ? null : rd.GetInt32(i); i++;
                int? cotId = rd.IsDBNull(i) ? null : rd.GetInt32(i); i++;

                decimal ToDecAndInc()
                {
                    if (rd.IsDBNull(i)) { i++; return 0m; }
                    var d = Convert.ToDecimal(rd.GetDouble(i), CultureInfo.InvariantCulture);
                    i++;
                    return d;
                }

                decimal prixA = ToDecAndInc();
                decimal prixV = ToDecAndInc();
                decimal stock = ToDecAndInc();
                decimal seuil = ToDecAndInc();
                decimal poids = ToDecAndInc();

                bool actif = rd.GetInt32(i++) == 1;

                string? cb;
                if (rd.IsDBNull(i)) { cb = null; i++; }
                else cb = rd.GetString(i++);

                var maj = ReadDateOnlyFromRecord(rd, i++);

                list.Add(new Article
                {
                    Id = id,
                    Code = code,
                    Libelle = libelle,
                    Type = type,
                    CategorieId = catId,
                    TvaRateId = tvaId,
                    CotisationRateId = cotId,
                    PrixAchatHT = prixA,
                    PrixVenteHT = prixV,
                    StockActuel = stock,
                    SeuilAlerte = seuil,
                    PoidsG = poids,
                    Actif = actif,
                    CodeBarres = cb,
                    DerniereMaj = maj
                });
            }

            Logger.Info($"GetAll -> {list.Count} article(s)");
            return list;
        }

        public Article? GetById(int id)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
SELECT Id, Code, Libelle, Type, CategorieId, TvaRateId, CotisationRateId,
       PrixAchatHT, PrixVenteHT, StockActuel, SeuilAlerte, PoidsG, Actif, CodeBarres, DerniereMaj
FROM Articles WHERE Id=@Id;";
            cmd.Parameters.AddWithValue("@Id", id);
            using var rd = cmd.ExecuteReader();
            return rd.Read() ? MapArticle(rd) : null;
        }

        public Article Insert(Article a)
        {
            using var cn = Db.Open();
            using var tx = cn.BeginTransaction();

            using (var cmd = cn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
INSERT INTO Articles
(Code, Libelle, Type, CategorieId, TvaRateId, CotisationRateId,
 PrixAchatHT, PrixVenteHT, StockActuel, SeuilAlerte, PoidsG, Actif, CodeBarres, DerniereMaj)
VALUES
(@Code, @Libelle, @Type, @CategorieId, @TvaRateId, @CotisationRateId,
 @PrixAchatHT, @PrixVenteHT, @StockActuel, @SeuilAlerte, @PoidsG, @Actif, @CodeBarres, @DerniereMaj);";

                cmd.Parameters.AddWithValue("@Code", a.Code ?? string.Empty);
                cmd.Parameters.AddWithValue("@Libelle", a.Libelle ?? string.Empty);
                cmd.Parameters.AddWithValue("@Type", (int)a.Type);
                cmd.Parameters.AddWithValue("@CategorieId", (object?)a.CategorieId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@TvaRateId", (object?)a.TvaRateId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CotisationRateId", (object?)a.CotisationRateId ?? DBNull.Value);

                cmd.Parameters.AddWithValue("@PrixAchatHT", Convert.ToDouble(a.PrixAchatHT));
                cmd.Parameters.AddWithValue("@PrixVenteHT", Convert.ToDouble(a.PrixVenteHT));
                cmd.Parameters.AddWithValue("@StockActuel", Convert.ToDouble(a.StockActuel));
                cmd.Parameters.AddWithValue("@SeuilAlerte", Convert.ToDouble(a.SeuilAlerte));
                cmd.Parameters.AddWithValue("@PoidsG", Convert.ToDouble(a.PoidsG));

                cmd.Parameters.AddWithValue("@Actif", a.Actif ? 1 : 0);
                cmd.Parameters.AddWithValue("@CodeBarres", (object?)a.CodeBarres ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@DerniereMaj", a.DerniereMaj.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                cmd.ExecuteNonQuery();
            }

            using (var idCmd = cn.CreateCommand())
            {
                idCmd.Transaction = tx;
                idCmd.CommandText = "SELECT last_insert_rowid();";
                var obj = idCmd.ExecuteScalar();
                a.Id = checked((int)Convert.ToInt64(obj));
            }

            tx.Commit();
            return a;
        }

        public void Update(Article a)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
UPDATE Articles SET
 Code=@Code, Libelle=@Libelle, Type=@Type, CategorieId=@CategorieId, TvaRateId=@TvaRateId, CotisationRateId=@CotisationRateId,
 PrixAchatHT=@PrixAchatHT, PrixVenteHT=@PrixVenteHT, StockActuel=@StockActuel, SeuilAlerte=@SeuilAlerte, PoidsG=@PoidsG,
 Actif=@Actif, CodeBarres=@CodeBarres, DerniereMaj=@DerniereMaj
WHERE Id=@Id;";
            cmd.Parameters.AddWithValue("@Id", a.Id);
            cmd.Parameters.AddWithValue("@Code", a.Code ?? string.Empty);
            cmd.Parameters.AddWithValue("@Libelle", a.Libelle ?? string.Empty);
            cmd.Parameters.AddWithValue("@Type", (int)a.Type);
            cmd.Parameters.AddWithValue("@CategorieId", (object?)a.CategorieId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@TvaRateId", (object?)a.TvaRateId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CotisationRateId", (object?)a.CotisationRateId ?? DBNull.Value);

            cmd.Parameters.AddWithValue("@PrixAchatHT", Convert.ToDouble(a.PrixAchatHT));
            cmd.Parameters.AddWithValue("@PrixVenteHT", Convert.ToDouble(a.PrixVenteHT));
            cmd.Parameters.AddWithValue("@StockActuel", Convert.ToDouble(a.StockActuel));
            cmd.Parameters.AddWithValue("@SeuilAlerte", Convert.ToDouble(a.SeuilAlerte));
            cmd.Parameters.AddWithValue("@PoidsG", Convert.ToDouble(a.PoidsG));

            cmd.Parameters.AddWithValue("@Actif", a.Actif ? 1 : 0);
            cmd.Parameters.AddWithValue("@CodeBarres", (object?)a.CodeBarres ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DerniereMaj", a.DerniereMaj.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "DELETE FROM Articles WHERE Id=@Id;";
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.ExecuteNonQuery();
        }

        private static Article MapArticle(IDataRecord rd)
        {
            int i = 0;

            var a = new Article();

            a.Id = rd.GetInt32(i++);
            a.Code = rd.IsDBNull(i) ? string.Empty : rd.GetString(i++);
            a.Libelle = rd.IsDBNull(i) ? string.Empty : rd.GetString(i++);
            a.Type = (ArticleType)rd.GetInt32(i++);

            a.CategorieId = rd.IsDBNull(i) ? (int?)null : rd.GetInt32(i++);
            a.TvaRateId = rd.IsDBNull(i) ? (int?)null : rd.GetInt32(i++);
            a.CotisationRateId = rd.IsDBNull(i) ? (int?)null : rd.GetInt32(i++);

            a.PrixAchatHT = rd.IsDBNull(i) ? 0m : Convert.ToDecimal(rd.GetDouble(i++), CultureInfo.InvariantCulture);
            a.PrixVenteHT = rd.IsDBNull(i) ? 0m : Convert.ToDecimal(rd.GetDouble(i++), CultureInfo.InvariantCulture);
            a.StockActuel = rd.IsDBNull(i) ? 0m : Convert.ToDecimal(rd.GetDouble(i++), CultureInfo.InvariantCulture);
            a.SeuilAlerte = rd.IsDBNull(i) ? 0m : Convert.ToDecimal(rd.GetDouble(i++), CultureInfo.InvariantCulture);
            a.PoidsG = rd.IsDBNull(i) ? 0m : Convert.ToDecimal(rd.GetDouble(i++), CultureInfo.InvariantCulture);

            a.Actif = rd.GetInt32(i++) != 0;

            a.CodeBarres = rd.IsDBNull(i) ? null : rd.GetString(i++);
            a.DerniereMaj = ReadDateOnlyFromRecord(rd, i++);

            return a;
        }

        // ======================================================
        //                      VARIANTES
        // ======================================================

        public List<ArticleVariant> GetVariants(int articleId)
        {
            var list = new List<ArticleVariant>();
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
SELECT Id, ArticleId, Nom,
       PrixAchatHT, PrixVenteHT, StockActuel, SeuilAlerte,
       CodeBarres
FROM ArticleVariants
WHERE ArticleId=@ArticleId
ORDER BY Id;";
            cmd.Parameters.AddWithValue("@ArticleId", articleId);
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                int i = 0;

                int id = rd.GetInt32(i++);
                int artId = rd.GetInt32(i++);
                string nom = rd.GetString(i++);

                decimal prixA = rd.IsDBNull(i) ? 0m : Convert.ToDecimal(rd.GetDouble(i++), CultureInfo.InvariantCulture);
                decimal prixV = rd.IsDBNull(i) ? 0m : Convert.ToDecimal(rd.GetDouble(i++), CultureInfo.InvariantCulture);
                decimal stock = rd.IsDBNull(i) ? 0m : Convert.ToDecimal(rd.GetDouble(i++), CultureInfo.InvariantCulture);
                decimal seuil = rd.IsDBNull(i) ? 0m : Convert.ToDecimal(rd.GetDouble(i++), CultureInfo.InvariantCulture);

                string? cb = rd.IsDBNull(i) ? null : rd.GetString(i++);

                list.Add(new ArticleVariant
                {
                    Id = id,
                    ArticleId = artId,
                    Nom = nom,
                    PrixAchatHT = prixA,
                    PrixVenteHT = prixV,
                    StockActuel = stock,
                    SeuilAlerte = seuil,
                    CodeBarres = cb
                });
            }
            return list;
        }

        public ArticleVariant InsertVariant(ArticleVariant v)
        {
            using var cn = Db.Open();
            using var tx = cn.BeginTransaction();

            using (var cmd = cn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
INSERT INTO ArticleVariants (ArticleId, Nom, PrixAchatHT, PrixVenteHT, StockActuel, SeuilAlerte, CodeBarres)
VALUES (@ArticleId, @Nom, @PrixAchatHT, @PrixVenteHT, @StockActuel, @SeuilAlerte, @CodeBarres);";
                cmd.Parameters.AddWithValue("@ArticleId", v.ArticleId);
                cmd.Parameters.AddWithValue("@Nom", v.Nom ?? string.Empty);
                cmd.Parameters.AddWithValue("@PrixAchatHT", Convert.ToDouble(v.PrixAchatHT));
                cmd.Parameters.AddWithValue("@PrixVenteHT", Convert.ToDouble(v.PrixVenteHT));
                cmd.Parameters.AddWithValue("@StockActuel", Convert.ToDouble(v.StockActuel));
                cmd.Parameters.AddWithValue("@SeuilAlerte", Convert.ToDouble(v.SeuilAlerte));
                cmd.Parameters.AddWithValue("@CodeBarres", (object?)v.CodeBarres ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }

            using (var idCmd = cn.CreateCommand())
            {
                idCmd.Transaction = tx;
                idCmd.CommandText = "SELECT last_insert_rowid();";
                v.Id = checked((int)Convert.ToInt64(idCmd.ExecuteScalar()));
            }

            tx.Commit();
            return v;
        }

        public void UpdateVariant(ArticleVariant v)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
UPDATE ArticleVariants
   SET Nom=@Nom,
       PrixAchatHT=@PrixAchatHT,
       PrixVenteHT=@PrixVenteHT,
       StockActuel=@StockActuel,
       SeuilAlerte=@SeuilAlerte,
       CodeBarres=@CodeBarres
 WHERE Id=@Id;";
            cmd.Parameters.AddWithValue("@Id", v.Id);
            cmd.Parameters.AddWithValue("@Nom", v.Nom ?? string.Empty);
            cmd.Parameters.AddWithValue("@PrixAchatHT", Convert.ToDouble(v.PrixAchatHT));
            cmd.Parameters.AddWithValue("@PrixVenteHT", Convert.ToDouble(v.PrixVenteHT));
            cmd.Parameters.AddWithValue("@StockActuel", Convert.ToDouble(v.StockActuel));
            cmd.Parameters.AddWithValue("@SeuilAlerte", Convert.ToDouble(v.SeuilAlerte));
            cmd.Parameters.AddWithValue("@CodeBarres", (object?)v.CodeBarres ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }


        public void DeleteVariant(int id)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "DELETE FROM ArticleVariants WHERE Id=@Id;";
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.ExecuteNonQuery();
        }

        // ======================================================
        //                       PACKS
        // ======================================================

        public List<PackItem> GetPackItems(int packArticleId)
        {
            var list = new List<PackItem>();
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
SELECT Id, PackArticleId, ArticleItemId, Quantite
FROM PackItems
WHERE PackArticleId=@PackArticleId
ORDER BY Id;";
            cmd.Parameters.AddWithValue("@PackArticleId", packArticleId);
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                int i = 0;

                int id = rd.GetInt32(i++);
                _ = rd.GetInt32(i++);
                int itemId = rd.GetInt32(i++);

                double qte = rd.IsDBNull(i) ? 0.0 : rd.GetDouble(i++);

                list.Add(new PackItem
                {
                    Id = id,
                    ArticleItemId = itemId,
                    Quantite = qte
                });
            }
            return list;
        }

        public void InsertPackItem(int packArticleId, int articleItemId, decimal quantite)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
INSERT INTO PackItems (PackArticleId, ArticleItemId, Quantite)
VALUES (@PackArticleId, @ArticleItemId, @Quantite);";
            cmd.Parameters.AddWithValue("@PackArticleId", packArticleId);
            cmd.Parameters.AddWithValue("@ArticleItemId", articleItemId);
            cmd.Parameters.AddWithValue("@Quantite", Convert.ToDouble(quantite));
            cmd.ExecuteNonQuery();
        }

        public void UpdatePackItem(int id, decimal quantite)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
UPDATE PackItems SET Quantite=@Quantite
WHERE Id=@Id;";
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@Quantite", Convert.ToDouble(quantite));
            cmd.ExecuteNonQuery();
        }

        public void DeletePackItem(int id)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "DELETE FROM PackItems WHERE Id=@Id;";
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.ExecuteNonQuery();
        }

        // ======================================================
        //                       IMAGES
        // ======================================================

        public void UpsertImage(int articleId, int slot, string relativePath)
        {
            if (slot < 1 || slot > 4) throw new ArgumentOutOfRangeException(nameof(slot));

            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
INSERT INTO ArticleImages (ArticleId, Slot, RelPath)
VALUES (@ArticleId, @Slot, @RelPath)
ON CONFLICT(ArticleId, Slot) DO UPDATE SET RelPath=excluded.RelPath;";
            cmd.Parameters.AddWithValue("@ArticleId", articleId);
            cmd.Parameters.AddWithValue("@Slot", slot);
            cmd.Parameters.AddWithValue("@RelPath", relativePath ?? string.Empty);
            cmd.ExecuteNonQuery();
        }

        public List<(int Slot, string RelPath)> GetImages(int articleId)
        {
            var list = new List<(int, string)>();
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
SELECT Slot, RelPath
FROM ArticleImages
WHERE ArticleId=@ArticleId
ORDER BY Slot;";
            cmd.Parameters.AddWithValue("@ArticleId", articleId);
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                list.Add((rd.GetInt32(0), rd.GetString(1)));
            }
            return list;
        }

        // conservé pour compat éventuelle ailleurs
        public List<string> GetImagePaths(int articleId)
        {
            var list = new List<string>();
            foreach (var it in GetImages(articleId))
                list.Add(it.RelPath);
            return list;
        }

        public void DeleteImage(int articleId, int slot)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
DELETE FROM ArticleImages
WHERE ArticleId=@ArticleId AND Slot=@Slot;";
            cmd.Parameters.AddWithValue("@ArticleId", articleId);
            cmd.Parameters.AddWithValue("@Slot", slot);
            cmd.ExecuteNonQuery();
        }

        // ------------ Helpers lecture date robuste --------------
        private static DateOnly ReadDateOnlyFromRecord(IDataRecord rd, int index)
        {
            var v = rd.GetValue(index);
            if (v == null || v == DBNull.Value)
                return DateOnly.FromDateTime(DateTime.Today);

            if (v is string s)
            {
                if (DateOnly.TryParseExact(s, "yyyy-MM-dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var d1))
                    return d1;

                if (DateOnly.TryParse(s, out var d2))
                    return d2;

                return DateOnly.FromDateTime(DateTime.Today);
            }

            if (v is DateTime dt) return DateOnly.FromDateTime(dt);

            if (v is long l)
            {
                try { return DateOnly.FromDateTime(DateTime.UnixEpoch.AddSeconds(l)); }
                catch { return DateOnly.FromDateTime(DateTime.Today); }
            }

            if (v is double d)
            {
                try { return DateOnly.FromDateTime(DateTime.UnixEpoch.AddSeconds((long)d)); }
                catch { return DateOnly.FromDateTime(DateTime.Today); }
            }

            return DateOnly.FromDateTime(DateTime.Today);
        }
    }
}
