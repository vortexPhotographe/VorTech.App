using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using VorTech.App;            // Db, Paths
using VorTech.App.Models;

namespace VorTech.App.Services
{
    public class ArticleService
    {
        // -----------------------------
        //  PUBLIC API (stabilisée)
        // -----------------------------
        public List<Article> GetAll(string? search = null)
        {
            var list = new List<Article>();
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();

            if (string.IsNullOrWhiteSpace(search))
            {
                cmd.CommandText = @"SELECT * FROM Articles ORDER BY Libelle COLLATE NOCASE";
            }
            else
            {
                cmd.CommandText = @"SELECT * FROM Articles WHERE Libelle LIKE @q ORDER BY Libelle COLLATE NOCASE";
                Db.AddParam(cmd, "@q", "%" + search + "%");
            }

            using var rd = cmd.ExecuteReader();
            while (rd.Read()) list.Add(ReadArticle(rd));
            return list;
        }

        public int Insert(Article a)
        {
            a.DerniereMaj = DateOnly.FromDateTime(DateTime.UtcNow);
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
INSERT INTO Articles(Code, Libelle, Type, CategorieId, TvaRateId, CotisationRateId,
                     PrixAchatHT, PrixVenteHT, StockActuel, SeuilAlerte, PoidsG, Actif, CodeBarres, DerniereMaj)
VALUES(@Code,@Libelle,@Type,@CategorieId,@TvaRateId,@CotisationRateId,
       @PrixAchatHT,@PrixVenteHT,@StockActuel,@SeuilAlerte,@PoidsG,@Actif,@CodeBarres,@DerniereMaj);
SELECT last_insert_rowid();";
            BindArticle(cmd, a);
            var id = Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
            return id;
        }

        public void Update(Article a)
        {
            a.DerniereMaj = DateOnly.FromDateTime(DateTime.UtcNow);
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
UPDATE Articles SET
    Code=@Code, Libelle=@Libelle, Type=@Type, CategorieId=@CategorieId,
    TvaRateId=@TvaRateId, CotisationRateId=@CotisationRateId,
    PrixAchatHT=@PrixAchatHT, PrixVenteHT=@PrixVenteHT,
    StockActuel=@StockActuel, SeuilAlerte=@SeuilAlerte,
    PoidsG=@PoidsG, Actif=@Actif, CodeBarres=@CodeBarres, DerniereMaj=@DerniereMaj
WHERE Id=@Id;";
            BindArticle(cmd, a);
            Db.AddParam(cmd, "@Id", a.Id);
            cmd.ExecuteNonQuery();
        }

        public bool Delete(int id)
        {
            // Refuser si référencé par PackItems (FK RESTRICT protège, mais on renvoie un bool propre)
            using var cn = Db.Open();
            using var chk = cn.CreateCommand();
            chk.CommandText = "SELECT 1 FROM PackItems WHERE ArticleItemId=@id LIMIT 1";
            Db.AddParam(chk, "@id", id);
            var used = chk.ExecuteScalar();
            if (used != null)
                return false; // l'appelant affichera un message lisible

            using var cmd = cn.CreateCommand();
            cmd.CommandText = "DELETE FROM Articles WHERE Id=@id";
            Db.AddParam(cmd, "@id", id);
            cmd.ExecuteNonQuery();
            return true;
        }

        // ---------- Variantes ----------
        public List<ArticleVariant> GetVariants(int articleId)
        {
            var list = new List<ArticleVariant>();
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT * FROM ArticleVariants WHERE ArticleId=@id ORDER BY Nom COLLATE NOCASE";
            Db.AddParam(cmd, "@id", articleId);
            using var rd = cmd.ExecuteReader();
            while (rd.Read()) list.Add(ReadVariant(rd));
            return list;
        }

        public void DeleteVariant(int variantId)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "DELETE FROM ArticleVariants WHERE Id=@Id";
            Db.AddParam(cmd, "@Id", variantId);
            cmd.ExecuteNonQuery();
            RecomputePacksAffectedByVariant(variantId);
        }

        public ArticleVariant? GetVariantById(int variantId)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT * FROM ArticleVariants WHERE Id=@Id";
            Db.AddParam(cmd, "@Id", variantId);
            using var rd = cmd.ExecuteReader();
            if (rd.Read()) return ReadVariant(rd);
            return null;
        }

        // ---------- Pack items ----------
        public List<PackItem> GetPackItems(int packArticleId)
        {
            var list = new List<PackItem>();
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT * FROM PackItems WHERE PackArticleId=@id ORDER BY Id";
            Db.AddParam(cmd, "@id", packArticleId);
            using var rd = cmd.ExecuteReader();
            while (rd.Read()) list.Add(ReadPackItem(rd));
            return list;
        }

        public void InsertPackItem(int packArticleId, int articleId, decimal quantite)
        {
            EnsureArticleIsNotPack(articleId);
            EnsureQuantiteValid(quantite);

            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"INSERT INTO PackItems(PackArticleId, ArticleItemId, Quantite, VariantId)
VALUES(@P,@A,@Q,NULL);";
            Db.AddParam(cmd, "@P", packArticleId);
            Db.AddParam(cmd, "@A", articleId);
            Db.AddParam(cmd, "@Q", quantite);
            cmd.ExecuteNonQuery();
            RecomputePackAggregates(packArticleId);
        }

        public void InsertPackItemVariant(int packArticleId, int articleId, int variantId, decimal quantite)
        {
            EnsureArticleIsNotPack(articleId);
            EnsureVariantBelongs(articleId, variantId);
            EnsureQuantiteValid(quantite);

            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"INSERT INTO PackItems(PackArticleId, ArticleItemId, Quantite, VariantId)
VALUES(@P,@A,@Q,@V);";
            Db.AddParam(cmd, "@P", packArticleId);
            Db.AddParam(cmd, "@A", articleId);
            Db.AddParam(cmd, "@Q", quantite);
            Db.AddParam(cmd, "@V", variantId);
            cmd.ExecuteNonQuery();
            RecomputePackAggregates(packArticleId);
        }

        public void UpsertPackItem(int packArticleId, int articleId, int? variantId, decimal quantite)
        {
            if (variantId.HasValue)
                InsertPackItemVariant(packArticleId, articleId, variantId.Value, quantite);
            else
                InsertPackItem(packArticleId, articleId, quantite);
        }

        public void UpdatePackItem(int packItemId, decimal quantite)
        {
            EnsureQuantiteValid(quantite);
            using var cn = Db.Open();
            using var get = cn.CreateCommand();
            get.CommandText = "SELECT PackArticleId FROM PackItems WHERE Id=@Id";
            Db.AddParam(get, "@Id", packItemId);
            var pid = Convert.ToInt32(get.ExecuteScalar() ?? 0);
            if (pid <= 0) return;

            using var cmd = cn.CreateCommand();
            cmd.CommandText = "UPDATE PackItems SET Quantite=@Q WHERE Id=@Id";
            Db.AddParam(cmd, "@Q", quantite);
            Db.AddParam(cmd, "@Id", packItemId);
            cmd.ExecuteNonQuery();
            RecomputePackAggregates(pid);
        }

        public void UpdatePackItemRef(int packItemId, int articleId, int? variantId)
        {
            EnsureArticleIsNotPack(articleId);
            using var cn = Db.Open();
            using var get = cn.CreateCommand();
            get.CommandText = "SELECT PackArticleId FROM PackItems WHERE Id=@Id";
            Db.AddParam(get, "@Id", packItemId);
            var pid = Convert.ToInt32(get.ExecuteScalar() ?? 0);
            if (pid <= 0) return;

            if (HasVariants(articleId) && variantId == null)
                throw new InvalidOperationException("Variant is required for articles that have variants");
            if (variantId != null) EnsureVariantBelongs(articleId, variantId.Value);

            using var cmd = cn.CreateCommand();
            cmd.CommandText = "UPDATE PackItems SET ArticleItemId=@A, VariantId=@V WHERE Id=@Id";
            Db.AddParam(cmd, "@A", articleId);
            Db.AddParam(cmd, "@V", (object?)variantId ?? DBNull.Value);
            Db.AddParam(cmd, "@Id", packItemId);
            cmd.ExecuteNonQuery();
            RecomputePackAggregates(pid);
        }

        public void DeletePackItem(int packItemId)
        {
            using var cn = Db.Open();
            using var get = cn.CreateCommand();
            get.CommandText = "SELECT PackArticleId FROM PackItems WHERE Id=@Id";
            Db.AddParam(get, "@Id", packItemId);
            var pid = Convert.ToInt32(get.ExecuteScalar() ?? 0);
            if (pid <= 0) return;

            using var cmd = cn.CreateCommand();
            cmd.CommandText = "DELETE FROM PackItems WHERE Id=@Id";
            Db.AddParam(cmd, "@Id", packItemId);
            cmd.ExecuteNonQuery();
            RecomputePackAggregates(pid);
        }

        public List<SelectablePackRow> GetSelectablePackRows()
        {
            // Retourne toutes les lignes possibles pour composer un pack :
            //  - articles simples SANS variantes
            //  - variantes (une ligne par variante) des articles qui en ont
            var list = new List<SelectablePackRow>();
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();

            // Articles sans variantes (Type=0) et sans lignes de variantes
            cmd.CommandText = @"
SELECT a.Id AS ArticleId, NULL AS VariantId, a.Libelle AS DisplayName
FROM Articles a
LEFT JOIN ArticleVariants v ON v.ArticleId = a.Id
WHERE a.Type = 0 AND v.Id IS NULL
ORDER BY a.Libelle COLLATE NOCASE;";
            using (var rd = cmd.ExecuteReader())
            {
                while (rd.Read())
                {
                    list.Add(new SelectablePackRow
                    {
                        ArticleId = Convert.ToInt32(rd["ArticleId"]),
                        VariantId = null,
                        DisplayName = Convert.ToString(rd["DisplayName"]) ?? string.Empty
                    });
                }
            }

            // Variantes des articles qui en ont
            cmd.CommandText = @"
SELECT a.Id AS ArticleId, v.Id AS VariantId, (a.Libelle || ' — ' || v.Nom) AS DisplayName
FROM Articles a
JOIN ArticleVariants v ON v.ArticleId = a.Id
WHERE a.Type = 0
ORDER BY a.Libelle COLLATE NOCASE, v.Nom COLLATE NOCASE;";
            using (var rd = cmd.ExecuteReader())
            {
                while (rd.Read())
                {
                    list.Add(new SelectablePackRow
                    {
                        ArticleId = Convert.ToInt32(rd["ArticleId"]),
                        VariantId = Convert.ToInt32(rd["VariantId"]),
                        DisplayName = Convert.ToString(rd["DisplayName"]) ?? string.Empty
                    });
                }
            }

            return list;
        }

        public List<(int PackItemId, string DisplayName, decimal Quantite)> GetPackItemsWithNames(int packArticleId)
        {
            var list = new List<(int, string, decimal)>();
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
SELECT pi.Id,
       CASE WHEN pi.VariantId IS NULL
            THEN a.Libelle
            ELSE (a.Libelle || ' — ' || v.Nom)
       END AS DisplayName,
       pi.Quantite
FROM PackItems pi
JOIN Articles a ON a.Id = pi.ArticleItemId
LEFT JOIN ArticleVariants v ON v.Id = pi.VariantId
WHERE pi.PackArticleId=@P
ORDER BY pi.Id;";
            Db.AddParam(cmd, "@P", packArticleId);
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                int id = Convert.ToInt32(rd["Id"]);
                string name = Convert.ToString(rd["DisplayName"]) ?? string.Empty;
                decimal qte = Convert.ToDecimal(rd["Quantite"], CultureInfo.InvariantCulture);
                list.Add((id, name, qte));
            }
            return list;
        }

        public void RecomputePacksAffectedByArticle(int articleId)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT DISTINCT PackArticleId FROM PackItems WHERE ArticleItemId=@A";
            Db.AddParam(cmd, "@A", articleId);
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                int packId = Convert.ToInt32(rd[0]);
                RecomputePackAggregates(packId);
            }
        }

        public void RecomputePacksAffectedByVariant(int variantId)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT DISTINCT PackArticleId FROM PackItems WHERE VariantId=@V";
            Db.AddParam(cmd, "@V", variantId);
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                int packId = Convert.ToInt32(rd[0]);
                RecomputePackAggregates(packId);
            }
        }

        public void RecomputePackAggregates(int packArticleId)
        {
            // 1) Charger la composition
            var items = GetPackItems(packArticleId);
            if (items == null || items.Count == 0)
            {
                UpdatePackComputed(packArticleId, 0m, 0m, 0m);
                return;
            }

            // 2) Calculs
            decimal achatTotal = 0m;
            decimal poidsTotal = 0m;
            decimal? minPacks = null;

            foreach (var it in items)
            {
                // Lecture composant
                int articleId = it.ArticleItemId;
                int? variantId = it.VariantId;
                decimal qte = Convert.ToDecimal(it.Quantite, CultureInfo.InvariantCulture);

                decimal pa, poids, stock;

                if (variantId.HasValue)
                {
                    var v = GetVariantById(variantId.Value);
                    if (v == null) continue; // ligne orpheline
                    var a = Get(articleId)!;
                    pa = v.PrixAchatHT;
                    poids = a.PoidsG;
                    stock = v.StockActuel;
                }
                else
                {
                    var a = Get(articleId)!;
                    pa = a.PrixAchatHT;
                    poids = a.PoidsG;
                    stock = a.StockActuel;
                }

                achatTotal += pa * qte;
                poidsTotal += poids * qte;
                var denom = (qte <= 0m) ? 1m : qte;                  // tout en decimal
                var packsPossible = Math.Floor(stock / denom);       // Math.Floor(decimal) -> decimal
                minPacks = minPacks.HasValue
                    ? Math.Min(minPacks.Value, packsPossible)
                    : packsPossible;
            }

            UpdatePackComputed(packArticleId, achatTotal, poidsTotal, minPacks ?? 0m);
        }

        // ---------- Images ----------
        public void UpsertImage(int articleId, int slot, string relPath)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
INSERT INTO ArticleImages(ArticleId, Slot, RelPath)
VALUES(@A,@S,@P)
ON CONFLICT(ArticleId, Slot) DO UPDATE SET RelPath=excluded.RelPath;";
            Db.AddParam(cmd, "@A", articleId);
            Db.AddParam(cmd, "@S", slot);
            Db.AddParam(cmd, "@P", relPath);
            cmd.ExecuteNonQuery();
        }

        public List<(int Slot, string RelPath, string FullPath)> GetImagePaths(int articleId)
        {
            var list = new List<(int, string, string)>();
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT Slot, RelPath FROM ArticleImages WHERE ArticleId=@A ORDER BY Slot";
            Db.AddParam(cmd, "@A", articleId);
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                int slot = Convert.ToInt32(rd["Slot"]);
                string rel = Convert.ToString(rd["RelPath"]) ?? string.Empty;
                string full = Path.Combine(Paths.AssetsDir, rel.Replace('/', Path.DirectorySeparatorChar));
                list.Add((slot, rel, full));
            }
            return list;
        }

        public void DeleteImage(int articleId, int slot)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "DELETE FROM ArticleImages WHERE ArticleId=@A AND Slot=@S";
            Db.AddParam(cmd, "@A", articleId);
            Db.AddParam(cmd, "@S", slot);
            cmd.ExecuteNonQuery();
        }

        // ----- BARCODE: existence check sur Articles et Variantes -----
        public bool BarcodeExists(string code, int? excludeArticleId = null)
        {
            if (string.IsNullOrWhiteSpace(code)) return false;

            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
SELECT 1
FROM Articles a
WHERE a.CodeBarres = @C
  AND (@X IS NULL OR a.Id <> @X)
UNION
SELECT 1
FROM ArticleVariants v
JOIN Articles a2 ON a2.Id = v.ArticleId
WHERE v.CodeBarres = @C
  AND (@X IS NULL OR a2.Id <> @X)
LIMIT 1;";
            Db.AddParam(cmd, "@C", code);
            Db.AddParam(cmd, "@X", excludeArticleId);

            using var rd = cmd.ExecuteReader();
            return rd.Read();
        }

        // ================= VARIANTES =================

        public List<ArticleVariant> GetVariantsByArticleId(int articleId)
        {
            var list = new List<ArticleVariant>();
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
SELECT Id, ArticleId, Nom, PrixAchatHT, PrixVenteHT, StockActuel, SeuilAlerte, CodeBarres
FROM ArticleVariants
WHERE ArticleId = @A
ORDER BY Id";
            Db.AddParam(cmd, "@A", articleId);
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                list.Add(new ArticleVariant
                {
                    Id = Convert.ToInt32(rd["Id"]),
                    ArticleId = Convert.ToInt32(rd["ArticleId"]),
                    Nom = rd["Nom"]?.ToString() ?? "",
                    PrixAchatHT = Convert.ToDecimal(rd["PrixAchatHT"]),
                    PrixVenteHT = Convert.ToDecimal(rd["PrixVenteHT"]),
                    StockActuel = Convert.ToDecimal(rd["StockActuel"]),
                    SeuilAlerte = Convert.ToDecimal(rd["SeuilAlerte"]),
                    CodeBarres = rd["CodeBarres"]?.ToString()
                });
            }
            return list;
        }

        public int InsertVariant(ArticleVariant v)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
INSERT INTO ArticleVariants(ArticleId, Nom, PrixAchatHT, PrixVenteHT, StockActuel, SeuilAlerte, CodeBarres)
VALUES(@ArticleId,@Nom,@PA,@PV,@Stock,@Seuil,@CB);
SELECT last_insert_rowid();";
            Db.AddParam(cmd, "@ArticleId", v.ArticleId);
            Db.AddParam(cmd, "@Nom", v.Nom ?? "");
            Db.AddParam(cmd, "@PA", v.PrixAchatHT);
            Db.AddParam(cmd, "@PV", v.PrixVenteHT);
            Db.AddParam(cmd, "@Stock", v.StockActuel);
            Db.AddParam(cmd, "@Seuil", v.SeuilAlerte);
            Db.AddParam(cmd, "@CB", (object?)v.CodeBarres ?? DBNull.Value);
            return Convert.ToInt32(cmd.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
        }

        public void UpdateVariant(ArticleVariant v)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
UPDATE ArticleVariants
SET Nom=@Nom, PrixAchatHT=@PA, PrixVenteHT=@PV, StockActuel=@Stock, SeuilAlerte=@Seuil, CodeBarres=@CB
WHERE Id=@Id;";
            Db.AddParam(cmd, "@Nom", v.Nom ?? "");
            Db.AddParam(cmd, "@PA", v.PrixAchatHT);
            Db.AddParam(cmd, "@PV", v.PrixVenteHT);
            Db.AddParam(cmd, "@Stock", v.StockActuel);
            Db.AddParam(cmd, "@Seuil", v.SeuilAlerte);
            Db.AddParam(cmd, "@CB", (object?)v.CodeBarres ?? DBNull.Value);
            Db.AddParam(cmd, "@Id", v.Id);
            cmd.ExecuteNonQuery();
        }

        // -----------------------------
        //  PRIVATE HELPERS
        // -----------------------------
        private Article? Get(int id)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT * FROM Articles WHERE Id=@Id";
            Db.AddParam(cmd, "@Id", id);
            using var rd = cmd.ExecuteReader();
            if (rd.Read()) return ReadArticle(rd);
            return null;
        }

        private static Article ReadArticle(IDataRecord rd)
        {
            return new Article
            {
                Id = Convert.ToInt32(rd["Id"]),
                Code = rd["Code"].ToString() ?? "",
                Libelle = rd["Libelle"].ToString() ?? "",
                Type = (ArticleType)Convert.ToInt32(rd["Type"]),
                CategorieId = rd["CategorieId"] as int? ?? (rd["CategorieId"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["CategorieId"])),
                TvaRateId = rd["TvaRateId"] as int? ?? (rd["TvaRateId"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["TvaRateId"])),
                CotisationRateId = rd["CotisationRateId"] as int? ?? (rd["CotisationRateId"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["CotisationRateId"])),
                PrixAchatHT = Convert.ToDecimal(rd["PrixAchatHT"], CultureInfo.InvariantCulture),
                PrixVenteHT = Convert.ToDecimal(rd["PrixVenteHT"], CultureInfo.InvariantCulture),
                StockActuel = Convert.ToDecimal(rd["StockActuel"], CultureInfo.InvariantCulture),
                SeuilAlerte = Convert.ToDecimal(rd["SeuilAlerte"], CultureInfo.InvariantCulture),
                PoidsG = Convert.ToDecimal(rd["PoidsG"], CultureInfo.InvariantCulture),
                Actif = Convert.ToInt32(rd["Actif"]) != 0,
                CodeBarres = rd["CodeBarres"].ToString(),
                DerniereMaj = DateOnly.TryParse(rd["DerniereMaj"]?.ToString(), out var d) ? d : DateOnly.FromDateTime(DateTime.UtcNow)
            };
        }

        private static void BindArticle(IDbCommand cmd, Article a)
        {
            Db.AddParam(cmd, "@Code", a.Code);
            Db.AddParam(cmd, "@Libelle", a.Libelle);
            Db.AddParam(cmd, "@Type", (int)a.Type);
            Db.AddParam(cmd, "@CategorieId", (object?)a.CategorieId ?? DBNull.Value);
            Db.AddParam(cmd, "@TvaRateId", (object?)a.TvaRateId ?? DBNull.Value);
            Db.AddParam(cmd, "@CotisationRateId", (object?)a.CotisationRateId ?? DBNull.Value);
            Db.AddParam(cmd, "@PrixAchatHT", a.PrixAchatHT);
            Db.AddParam(cmd, "@PrixVenteHT", a.PrixVenteHT);
            Db.AddParam(cmd, "@StockActuel", a.StockActuel);
            Db.AddParam(cmd, "@SeuilAlerte", a.SeuilAlerte);
            Db.AddParam(cmd, "@PoidsG", a.PoidsG);
            Db.AddParam(cmd, "@Actif", a.Actif ? 1 : 0);
            Db.AddParam(cmd, "@CodeBarres", (object?)a.CodeBarres ?? DBNull.Value);
            Db.AddParam(cmd, "@DerniereMaj", a.DerniereMaj.ToString("yyyy-MM-dd"));
        }

        private static ArticleVariant ReadVariant(IDataRecord rd)
        {
            return new ArticleVariant
            {
                Id = Convert.ToInt32(rd["Id"]),
                ArticleId = Convert.ToInt32(rd["ArticleId"]),
                Nom = rd["Nom"].ToString() ?? "",
                PrixVenteHT = Convert.ToDecimal(rd["PrixVenteHT"], CultureInfo.InvariantCulture),
                CodeBarres = rd["CodeBarres"].ToString() ?? string.Empty,
                PrixAchatHT = Convert.ToDecimal(rd["PrixAchatHT"], CultureInfo.InvariantCulture),
                StockActuel = Convert.ToDecimal(rd["StockActuel"], CultureInfo.InvariantCulture),
                SeuilAlerte = Convert.ToDecimal(rd["SeuilAlerte"], CultureInfo.InvariantCulture)
            };
        }

        private static void BindVariant(IDbCommand cmd, ArticleVariant v)
        {
            Db.AddParam(cmd, "@ArticleId", v.ArticleId);
            Db.AddParam(cmd, "@Nom", v.Nom);
            Db.AddParam(cmd, "@PrixVenteHT", v.PrixVenteHT);
            Db.AddParam(cmd, "@CodeBarres", v.CodeBarres);
            Db.AddParam(cmd, "@PrixAchatHT", v.PrixAchatHT);
            Db.AddParam(cmd, "@StockActuel", v.StockActuel);
            Db.AddParam(cmd, "@SeuilAlerte", v.SeuilAlerte);
        }

        private static PackItem ReadPackItem(IDataRecord rd)
        {
            return new PackItem
            {
                Id = Convert.ToInt32(rd["Id"]),
                ArticlePackId = Convert.ToInt32(rd["PackArticleId"]),  // colonne SQL -> propriété modèle
                ArticleItemId = Convert.ToInt32(rd["ArticleItemId"]),
                VariantId = rd["VariantId"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["VariantId"]),
                Quantite = Convert.ToDouble(rd["Quantite"], CultureInfo.InvariantCulture) // modèle = double
            };
        }

        private void UpdatePackComputed(int packArticleId, decimal achat, decimal poids, decimal stock)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"UPDATE Articles SET PrixAchatHT=@Achat, PoidsG=@Poids, StockActuel=@Stock WHERE Id=@Id";
            Db.AddParam(cmd, "@Achat", achat);
            Db.AddParam(cmd, "@Poids", poids);
            Db.AddParam(cmd, "@Stock", stock);
            Db.AddParam(cmd, "@Id", packArticleId);
            cmd.ExecuteNonQuery();
        }

        private static void EnsureQuantiteValid(decimal qte)
        {
            if (qte < 1) throw new InvalidOperationException("Quantite must be >= 1");
        }

        private static void RequireVariantBarcode(ArticleVariant v)
        {
            if (string.IsNullOrWhiteSpace(v.CodeBarres))
                throw new InvalidOperationException("Code-barres obligatoire pour une variante");
        }

        private static void EnsureArticleIsNotPack(int articleId)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT Type FROM Articles WHERE Id=@Id";
            Db.AddParam(cmd, "@Id", articleId);
            var t = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
            if (t == 1) throw new InvalidOperationException("A pack cannot contain another pack");
        }

        private static void EnsureVariantBelongs(int articleId, int variantId)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT 1 FROM ArticleVariants WHERE Id=@V AND ArticleId=@A";
            Db.AddParam(cmd, "@V", variantId);
            Db.AddParam(cmd, "@A", articleId);
            var ok = cmd.ExecuteScalar();
            if (ok == null) throw new InvalidOperationException("VariantId does not belong to ArticleItemId");
        }

        private bool HasVariants(int articleId)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT 1 FROM ArticleVariants WHERE ArticleId=@A LIMIT 1";
            Db.AddParam(cmd, "@A", articleId);
            return cmd.ExecuteScalar() != null;
        }

        // DTO pour SelectPackItemsWindow
        public class SelectablePackRow
        {
            public int ArticleId { get; set; }
            public int? VariantId { get; set; }
            public string DisplayName { get; set; } = "";
        }
    }
}