using DocumentFormat.OpenXml.Office2010.Excel;
using DocumentFormat.OpenXml.VariantTypes;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Windows;
using VorTech.App;            // Db, Logger, Paths
using VorTech.App.Models;
using VorTech.App.Views;
using static VorTech.App.Views.ArticlesView;


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
    VariantId      INTEGER NULL,
    Quantite       REAL NOT NULL DEFAULT 1,
    FOREIGN KEY (PackArticleId) REFERENCES Articles(Id) ON DELETE CASCADE,
    FOREIGN KEY (ArticleItemId) REFERENCES Articles(Id) ON DELETE RESTRICT
);";
            cmd.ExecuteNonQuery();
            try
            {
                cmd.CommandText = "ALTER TABLE PackItems ADD COLUMN VariantId INTEGER NULL;";
                cmd.ExecuteNonQuery();
            }
            catch { /* colonne déjà existante => ignore */ }

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

            // helpers locaux pour garantir l’incrément dans tous les cas
            int? ReadNullableInt()
            {
                int? v = rd.IsDBNull(i) ? (int?)null : rd.GetInt32(i);
                i++;
                return v;
            }

            string? ReadNullableString()
            {
                string? v = rd.IsDBNull(i) ? null : rd.GetString(i);
                i++;
                return v;
            }

            decimal ReadDecimal()
            {
                // colonnes REAL -> double -> decimal
                var v = Convert.ToDecimal(rd.GetDouble(i), CultureInfo.InvariantCulture);
                i++;
                return v;
            }

            var a = new Article
            {
                Id = rd.GetInt32(i++),
                Code = rd.GetString(i++),
                Libelle = rd.GetString(i++),
                Type = (ArticleType)rd.GetInt32(i++),

                CategorieId = ReadNullableInt(),
                TvaRateId = ReadNullableInt(),
                CotisationRateId = ReadNullableInt(),

                PrixAchatHT = ReadDecimal(),
                PrixVenteHT = ReadDecimal(),
                StockActuel = ReadDecimal(),
                SeuilAlerte = ReadDecimal(),
                PoidsG = ReadDecimal(),

                // SQLite INTEGER 0/1 -> bool
                Actif = rd.GetInt32(i++) != 0,

                CodeBarres = ReadNullableString(),

                // DerniereMaj : TEXT "yyyy-MM-dd"
                DerniereMaj = DateOnly.Parse(rd.GetString(i++), CultureInfo.InvariantCulture)
            };

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
SELECT Id, PackArticleId, ArticleItemId, VariantId, Quantite
FROM PackItems
WHERE PackArticleId=@PackArticleId
ORDER BY Id;";
            cmd.Parameters.AddWithValue("@PackArticleId", packArticleId);
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                int i = 0;

                int id = rd.GetInt32(i++);        // Id
                _ = rd.GetInt32(i++);             // PackArticleId (non utilisé ici)
                int itemId = rd.GetInt32(i++);    // ArticleItemId
                int? variantId = rd.IsDBNull(i) ? (int?)null : rd.GetInt32(i++);  // VariantId
                double qte = rd.IsDBNull(i) ? 0.0 : rd.GetDouble(i++);            // Quantite

                list.Add(new PackItem
                {
                    Id = id,
                    ArticleItemId = itemId,
                    VariantId = variantId,
                    Quantite = qte
                });
            }
            return list;
        }

        public List<SelectablePackRow> GetSelectablePackRows()
        {
            var list = new List<SelectablePackRow>();
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            // On liste tous les ARTICLES (Type=Article) et leurs variantes.
            // Si tu veux filtrer "Actif", ajoute "AND a.Actif=1" dans les 2 SELECT.
            cmd.CommandText = @"
SELECT a.Id AS ArticleId,
       NULL AS VariantId,
       a.Libelle AS DisplayName
FROM Articles a
WHERE a.Type = @TypeArticle

UNION ALL

SELECT v.ArticleId AS ArticleId,
       v.Id AS VariantId,
       a.Libelle || ' – ' || v.Nom AS DisplayName
FROM ArticleVariants v
JOIN Articles a ON a.Id = v.ArticleId
WHERE a.Type = @TypeArticle
ORDER BY DisplayName;";
            // ArticleType.Article = 0 (si ton enum diffère, mets la bonne valeur)
            cmd.Parameters.AddWithValue("@TypeArticle", 0);

            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                int artId = rd.GetInt32(0);
                int? varId = rd.IsDBNull(1) ? (int?)null : rd.GetInt32(1);
                string name = rd.GetString(2);

                list.Add(new SelectablePackRow
                {
                    ArticleId = artId,
                    VariantId = varId,
                    DisplayName = name
                });
            }
            return list;
        }

        // =======================================================================
        // ===============   AGRÉGATS PACK : prix/poids/stock   ==================
        // =======================================================================

        public void RecomputePackAggregates(int packArticleId)
        {
            // 1) Charger la composition
            var items = GetPackItems(packArticleId);

            // Si pack vide => tout à 0
            if (items == null || items.Count == 0)
            {
                UpdatePackComputed(packArticleId, 0m, 0m, 0m);
                return;
            }

            // 2) Calculs
            decimal achat = 0m;
            decimal poids = 0m;
            decimal? minPacks = null;

            foreach (var it in items)
            {
                // lecture composant
                decimal pa, pds, stock;

                if (it.VariantId.HasValue && it.VariantId.Value > 0)
                {
                    // -> composant = VARIANTE
                    var v = GetVariantById(it.VariantId.Value);
                    var parent = GetById(v.ArticleId) ?? throw new Exception($"Article {v.ArticleId} introuvable");
                    pa = v.PrixAchatHT;   // prix achat déclinaison
                    pds = parent.PoidsG;   // poids depuis l'article
                    stock = v.StockActuel;   // stock déclinaison
                }
                else
                {
                    // -> composant = ARTICLE
                    var a = GetById(it.ArticleItemId) ?? throw new Exception($"Article {it.ArticleItemId} introuvable");
                    pa = a.PrixAchatHT;
                    pds = a.PoidsG;
                    stock = a.StockActuel;
                }

                var q = (decimal)it.Quantite;
                achat += pa * q;
                poids += pds * q;

                var packsFromThis = (q <= 0m) ? 0m : Math.Floor(stock / q);
                minPacks = (minPacks == null) ? packsFromThis : Math.Min(minPacks.Value, packsFromThis);
            }

            // 3) Règle 1-ligne : si une seule ligne, quantité >= 2 sinon pack indisponible
            if (items.Count == 1 && items[0].Quantite < 2)
            {
                minPacks = 0m;
            }

            UpdatePackComputed(packArticleId, achat, poids, minPacks ?? 0m);
        }

        // met à jour l'article pack (prix achat / poids / stock)
        private void UpdatePackComputed(int packArticleId, decimal prixAchat, decimal poidsG, decimal stock)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
UPDATE Articles
   SET PrixAchatHT=@PA, PoidsG=@Pds, StockActuel=@Stock, DerniereMaj=@Maj
 WHERE Id=@Id;";
            cmd.Parameters.AddWithValue("@Id", packArticleId);
            cmd.Parameters.AddWithValue("@PA", Convert.ToDouble(prixAchat));
            cmd.Parameters.AddWithValue("@Pds", Convert.ToDouble(poidsG));
            cmd.Parameters.AddWithValue("@Stock", Convert.ToDouble(stock));
            cmd.Parameters.AddWithValue("@Maj", DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            cmd.ExecuteNonQuery();
        }

        public ArticleVariant GetVariantById(int id)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
SELECT Id, ArticleId, Nom, PrixAchatHT, PrixVenteHT, StockActuel, SeuilAlerte, CodeBarres
FROM ArticleVariants WHERE Id=@Id;";
            cmd.Parameters.AddWithValue("@Id", id);
            using var rd = cmd.ExecuteReader();
            if (rd.Read())
            {
                int i = 0;
                return new ArticleVariant
                {
                    Id = rd.GetInt32(i++),
                    ArticleId = rd.GetInt32(i++),
                    Nom = rd.GetString(i++),
                    PrixAchatHT = Convert.ToDecimal(rd.GetDouble(i++), CultureInfo.InvariantCulture),
                    PrixVenteHT = Convert.ToDecimal(rd.GetDouble(i++), CultureInfo.InvariantCulture),
                    StockActuel = Convert.ToDecimal(rd.GetDouble(i++), CultureInfo.InvariantCulture),
                    SeuilAlerte = Convert.ToDecimal(rd.GetDouble(i++), CultureInfo.InvariantCulture),
                    CodeBarres = rd.IsDBNull(i) ? null : rd.GetString(i++)
                };
            }
            throw new Exception($"Variante {id} introuvable");
        }

        // --- Propagation des changements composants vers les packs ---
        public void RecomputePacksAffectedByArticle(int articleId)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
SELECT DISTINCT PackArticleId
FROM PackItems
WHERE ArticleItemId=@ArticleId;";
            cmd.Parameters.AddWithValue("@ArticleId", articleId);
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                RecomputePackAggregates(rd.GetInt32(0));
            }
        }

        public void RecomputePacksAffectedByVariant(int variantId)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
SELECT DISTINCT PackArticleId
FROM PackItems
WHERE VariantId=@VariantId;";
            cmd.Parameters.AddWithValue("@VariantId", variantId);
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                RecomputePackAggregates(rd.GetInt32(0));
            }
        }


        public void InsertPackItem(int packArticleId, int articleItemId, decimal quantite)
        {
            if (articleItemId <= 0)
                throw new Exception("Pack : aucun article sélectionné (ArticleItemId <= 0).");

            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
INSERT INTO PackItems (PackArticleId, ArticleItemId, Quantite)
VALUES (@Pack, @Article, @Qte);";
            cmd.Parameters.AddWithValue("@Pack", packArticleId);
            cmd.Parameters.AddWithValue("@Article", articleItemId);
            cmd.Parameters.AddWithValue("@Qte", Convert.ToDouble(quantite));
            cmd.ExecuteNonQuery();
        }

        public void InsertPackItemVariant(int packArticleId, int variantId, decimal quantite)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();

            // 1) Récupérer l'article parent de la variante (pour satisfaire le NOT NULL + FK)
            int parentArticleId;
            using (var cmdGet = cn.CreateCommand())
            {
                cmdGet.CommandText = "SELECT ArticleId FROM ArticleVariants WHERE Id=@Id;";
                cmdGet.Parameters.AddWithValue("@Id", variantId);
                var o = cmdGet.ExecuteScalar();
                if (o == null) throw new Exception($"Variante {variantId} introuvable");
                parentArticleId = Convert.ToInt32(o);
            }

            // 2) Insérer la ligne de pack avec ArticleItemId=parent, VariantId=variante
            cmd.CommandText = @"
INSERT INTO PackItems (PackArticleId, ArticleItemId, VariantId, Quantite)
VALUES (@Pack, @Article, @Variant, @Qte);";
            cmd.Parameters.AddWithValue("@Pack", packArticleId);
            cmd.Parameters.AddWithValue("@Article", parentArticleId);
            cmd.Parameters.AddWithValue("@Variant", variantId);
            cmd.Parameters.AddWithValue("@Qte", Convert.ToDouble(quantite));
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

        public void UpdatePackItemRef(int id, int? articleItemId, int? variantId)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
UPDATE PackItems
   SET ArticleItemId = @ArticleItemId,
       VariantId     = @VariantId
 WHERE Id=@Id;";
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@ArticleItemId", (object?)articleItemId ?? 0);
            cmd.Parameters.AddWithValue("@VariantId", (object?)variantId ?? DBNull.Value);
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

        public List<PackItemWithName> GetPackItemsWithNames(int packArticleId)
        {
            var list = new List<PackItemWithName>();
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
SELECT  pi.Id,
        pi.PackArticleId,
        pi.ArticleItemId,
        pi.VariantId,
        pi.Quantite,
        a.Libelle            AS ArticleName,
        v.Nom                AS VariantName
FROM PackItems pi
JOIN Articles a           ON a.Id = pi.ArticleItemId
LEFT JOIN ArticleVariants v ON v.Id = pi.VariantId
WHERE pi.PackArticleId = @Pack
ORDER BY pi.Id;";
            cmd.Parameters.AddWithValue("@Pack", packArticleId);

            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                int i = 0;
                var id = rd.GetInt32(i++);
                var pack = rd.GetInt32(i++);
                var artId = rd.GetInt32(i++);
                int? varId = rd.IsDBNull(i) ? (int?)null : rd.GetInt32(i++);
                var qte = rd.IsDBNull(i) ? 0.0 : rd.GetDouble(i++);
                var artNm = rd.IsDBNull(i) ? "" : rd.GetString(i++);
                var varNm = rd.IsDBNull(i) ? "" : rd.GetString(i++);

                var display = string.IsNullOrWhiteSpace(varNm) ? artNm : $"{artNm} – {varNm}";

                list.Add(new PackItemWithName
                {
                    Id = id,
                    PackArticleId = pack,
                    ArticleItemId = artId,
                    VariantId = varId,
                    Quantite = qte,
                    DisplayName = display
                });
            }
            return list;
        }

        // DTO utilisé par la fenêtre de sélection
        public class SelectablePackRow
        {
            public int ArticleId { get; set; }      // Article parent
            public int? VariantId { get; set; }     // null si "article" (pas de déclinaison)
            public string DisplayName { get; set; } = "";
        }

        public void UpsertPackItem(int packArticleId, int articleId, int? variantId, decimal quantite)
        {
            using var cn = Db.Open();

            // 1) Chercher une ligne existante (même trio Pack/Article/Variant)
            int? existingId = null;
            using (var cmdFind = cn.CreateCommand())
            {
                cmdFind.CommandText = @"
SELECT Id FROM PackItems
WHERE PackArticleId=@Pack AND ArticleItemId=@Art
  AND ((@Var IS NULL AND VariantId IS NULL) OR VariantId=@Var);";
                cmdFind.Parameters.AddWithValue("@Pack", packArticleId);
                cmdFind.Parameters.AddWithValue("@Art", articleId);
                cmdFind.Parameters.AddWithValue("@Var", (object?)variantId ?? DBNull.Value);
                var o = cmdFind.ExecuteScalar();
                if (o != null && o != DBNull.Value) existingId = Convert.ToInt32(o);
            }

            if (existingId.HasValue)
            {
                // 2) Si existe → incrémente la quantité
                using var cmdUp = cn.CreateCommand();
                cmdUp.CommandText = "UPDATE PackItems SET Quantite = Quantite + @Qte WHERE Id=@Id;";
                cmdUp.Parameters.AddWithValue("@Qte", Convert.ToDouble(quantite));
                cmdUp.Parameters.AddWithValue("@Id", existingId.Value);
                cmdUp.ExecuteNonQuery();
            }
            else
            {
                // 3) Sinon → insert
                if (variantId.HasValue && variantId.Value > 0)
                {
                    // récupérer article parent pour satisfaire la FK ArticleItemId NOT NULL
                    int parentArticleId;
                    using (var cmdGet = cn.CreateCommand())
                    {
                        cmdGet.CommandText = "SELECT ArticleId FROM ArticleVariants WHERE Id=@Id;";
                        cmdGet.Parameters.AddWithValue("@Id", variantId.Value);
                        var o = cmdGet.ExecuteScalar() ?? throw new Exception($"Variante {variantId.Value} introuvable");
                        parentArticleId = Convert.ToInt32(o);
                    }

                    using var cmdInsV = cn.CreateCommand();
                    cmdInsV.CommandText = @"
INSERT INTO PackItems (PackArticleId, ArticleItemId, VariantId, Quantite)
VALUES (@Pack, @Art, @Var, @Qte);";
                    cmdInsV.Parameters.AddWithValue("@Pack", packArticleId);
                    cmdInsV.Parameters.AddWithValue("@Art", parentArticleId);
                    cmdInsV.Parameters.AddWithValue("@Var", variantId.Value);
                    cmdInsV.Parameters.AddWithValue("@Qte", Convert.ToDouble(quantite));
                    cmdInsV.ExecuteNonQuery();
                }
                else
                {
                    using var cmdIns = cn.CreateCommand();
                    cmdIns.CommandText = @"
INSERT INTO PackItems (PackArticleId, ArticleItemId, Quantite)
VALUES (@Pack, @Art, @Qte);";
                    cmdIns.Parameters.AddWithValue("@Pack", packArticleId);
                    cmdIns.Parameters.AddWithValue("@Art", articleId);
                    cmdIns.Parameters.AddWithValue("@Qte", Convert.ToDouble(quantite));
                    cmdIns.ExecuteNonQuery();
                }
            }
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
    public class PackItemWithName
    {
        public int Id { get; set; }
        public int PackArticleId { get; set; }
        public int ArticleItemId { get; set; }
        public int? VariantId { get; set; }
        public double Quantite { get; set; }
        public string DisplayName { get; set; } = "";
    }
}
