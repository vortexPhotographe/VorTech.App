using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using VorTech.App.Models;

namespace VorTech.App.Services
{
    public class ArticleService
    {
        private readonly string _dbPath;

        public ArticleService()
        {
            _dbPath = Path.Combine(Paths.DataDir, "app.db");
            Directory.CreateDirectory(Paths.DataDir);
            EnsureSchema();
        }

        private SqliteConnection Open()
        {
            var cn = new SqliteConnection($"Data Source={_dbPath};Cache=Shared");
            cn.Open();
            return cn;
        }

        private void EnsureSchema()
        {
            using var cn = Open();
            using var tx = cn.BeginTransaction();

            var drop = @"
                DROP TABLE IF EXISTS PackItems;
                DROP TABLE IF EXISTS ArticleImages;
                DROP TABLE IF EXISTS ArticleVariants;
                DROP TABLE IF EXISTS Articles;
            ";
            var create = @"
                CREATE TABLE IF NOT EXISTS Articles(
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Code TEXT NOT NULL UNIQUE,
                    Libelle TEXT NOT NULL,
                    Type INTEGER NOT NULL,
                    CategorieId INTEGER NULL,
                    TvaRateId INTEGER NULL,
                    CotisationRateId INTEGER NULL,
                    PrixAchatHT REAL NOT NULL,
                    PrixVenteHT REAL NOT NULL,
                    StockActuel REAL NOT NULL DEFAULT 0,
                    SeuilAlerte REAL NOT NULL DEFAULT 0,
                    PoidsG REAL NOT NULL DEFAULT 0,
                    Actif INTEGER NOT NULL DEFAULT 1,
                    CodeBarres TEXT NULL,
                    DerniereMaj TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS ArticleVariants(
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ArticleId INTEGER NOT NULL,
                    Nom TEXT NOT NULL,
                    PrixVenteHT REAL NOT NULL,
                    CodeBarres TEXT NULL,
                    ImagePath TEXT NULL,
                    FOREIGN KEY(ArticleId) REFERENCES Articles(Id) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS ArticleImages(
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ArticleId INTEGER NOT NULL,
                    Ordre INTEGER NOT NULL,
                    Path TEXT NOT NULL,
                    FOREIGN KEY(ArticleId) REFERENCES Articles(Id) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS PackItems(
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ArticlePackId INTEGER NOT NULL,
                    ArticleItemId INTEGER NOT NULL,
                    Quantite REAL NOT NULL,
                    FOREIGN KEY(ArticlePackId) REFERENCES Articles(Id) ON DELETE CASCADE,
                    FOREIGN KEY(ArticleItemId) REFERENCES Articles(Id)
                );

                CREATE UNIQUE INDEX IF NOT EXISTS UX_ArticleImages_AO ON ArticleImages(ArticleId, Ordre);
            ";
            new SqliteCommand(drop, cn, tx).ExecuteNonQuery();
            new SqliteCommand(create, cn, tx).ExecuteNonQuery();

            tx.Commit();
        }

        // ---------- CRUD ARTICLES ----------
        public IEnumerable<Article> GetAll()
        {
            using var cn = Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
SELECT Id, Code, Libelle, Type, CategorieId, TvaRateId, CotisationRateId,
       PrixAchatHT, PrixVenteHT, StockActuel, SeuilAlerte, PoidsG, Actif, CodeBarres, DerniereMaj
FROM Articles
ORDER BY Id DESC;";
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                yield return new Article
                {
                    Id = rd.GetInt32(0),
                    Code = rd.GetString(1),
                    Libelle = rd.GetString(2),
                    Type = (ArticleType)rd.GetInt32(3),
                    CategorieId = rd.IsDBNull(4) ? null : rd.GetInt32(4),
                    TvaRateId = rd.IsDBNull(5) ? null : rd.GetInt32(5),
                    CotisationRateId = rd.IsDBNull(6) ? null : rd.GetInt32(6),
                    PrixAchatHT = (decimal)rd.GetDouble(7),
                    PrixVenteHT = (decimal)rd.GetDouble(8),
                    StockActuel = (decimal)rd.GetDouble(9),
                    SeuilAlerte = (decimal)rd.GetDouble(10),
                    PoidsG = (decimal)rd.GetDouble(11),
                    Actif = rd.GetInt32(12) == 1,
                    CodeBarres = rd.IsDBNull(13) ? null : rd.GetString(13),
                    DerniereMaj = DateOnly.Parse(rd.GetString(14))
                };
            }
        }

        public Article Insert(Article a)
        {
            using var cn = Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
INSERT INTO Articles(Code, Libelle, Type, CategorieId, TvaRateId, CotisationRateId,
                     PrixAchatHT, PrixVenteHT, StockActuel, SeuilAlerte, PoidsG, Actif, CodeBarres, DerniereMaj)
VALUES ($Code,$Lib,$Type,$Cat,$Tva,$Cot,$PA,$PV,$Stock,$Seuil,$Poids,$Actif,$CB,$Maj);
SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("$Code", a.Code);
            cmd.Parameters.AddWithValue("$Lib", a.Libelle);
            cmd.Parameters.AddWithValue("$Type", (int)a.Type);
            cmd.Parameters.AddWithValue("$Cat", (object?)a.CategorieId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$Tva", (object?)a.TvaRateId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$Cot", (object?)a.CotisationRateId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$PA", System.Convert.ToDouble(a.PrixAchatHT));
            cmd.Parameters.AddWithValue("$PV", System.Convert.ToDouble(a.PrixVenteHT));
            cmd.Parameters.AddWithValue("$Stock", System.Convert.ToDouble(a.StockActuel));
            cmd.Parameters.AddWithValue("$Seuil", System.Convert.ToDouble(a.SeuilAlerte));
            cmd.Parameters.AddWithValue("$Poids", System.Convert.ToDouble(a.PoidsG));
            cmd.Parameters.AddWithValue("$Actif", a.Actif ? 1 : 0);
            cmd.Parameters.AddWithValue("$CB", (object?)a.CodeBarres ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$Maj", a.DerniereMaj.ToString("yyyy-MM-dd"));
            a.Id = (int)(long)cmd.ExecuteScalar()!;
            return a;
        }

        public void Update(Article a)
        {
            using var cn = Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
UPDATE Articles SET
    Code=$Code, Libelle=$Lib, Type=$Type, CategorieId=$Cat, TvaRateId=$Tva, CotisationRateId=$Cot,
    PrixAchatHT=$PA, PrixVenteHT=$PV, StockActuel=$Stock, SeuilAlerte=$Seuil, PoidsG=$Poids,
    Actif=$Actif, CodeBarres=$CB, DerniereMaj=$Maj
WHERE Id=$Id;";
            cmd.Parameters.AddWithValue("$Id", a.Id);
            cmd.Parameters.AddWithValue("$Code", a.Code);
            cmd.Parameters.AddWithValue("$Lib", a.Libelle);
            cmd.Parameters.AddWithValue("$Type", (int)a.Type);
            cmd.Parameters.AddWithValue("$Cat", (object?)a.CategorieId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$Tva", (object?)a.TvaRateId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$Cot", (object?)a.CotisationRateId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$PA", System.Convert.ToDouble(a.PrixAchatHT));
            cmd.Parameters.AddWithValue("$PV", System.Convert.ToDouble(a.PrixVenteHT));
            cmd.Parameters.AddWithValue("$Stock", System.Convert.ToDouble(a.StockActuel));
            cmd.Parameters.AddWithValue("$Seuil", System.Convert.ToDouble(a.SeuilAlerte));
            cmd.Parameters.AddWithValue("$Poids", System.Convert.ToDouble(a.PoidsG));
            cmd.Parameters.AddWithValue("$Actif", a.Actif ? 1 : 0);
            cmd.Parameters.AddWithValue("$CB", (object?)a.CodeBarres ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$Maj", a.DerniereMaj.ToString("yyyy-MM-dd"));
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            using var cn = Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "DELETE FROM Articles WHERE Id=$Id;";
            cmd.Parameters.AddWithValue("$Id", id);
            cmd.ExecuteNonQuery();
        }

        // ---------- VARIANTS ----------
        public IEnumerable<ArticleVariant> GetVariants(int articleId)
        {
            using var cn = Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
SELECT Id, ArticleId, Nom, PrixVenteHT, CodeBarres, ImagePath
FROM ArticleVariants
WHERE ArticleId=$A
ORDER BY Id;";
            cmd.Parameters.AddWithValue("$A", articleId);
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                yield return new ArticleVariant
                {
                    Id = rd.GetInt32(0),
                    ArticleId = rd.GetInt32(1),
                    Nom = rd.GetString(2),
                    PrixVenteHT = (decimal)rd.GetDouble(3),
                    CodeBarres = rd.IsDBNull(4) ? null : rd.GetString(4),
                    ImagePath = rd.IsDBNull(5) ? null : rd.GetString(5)
                };
            }
        }

        public ArticleVariant InsertVariant(ArticleVariant v)
        {
            using var cn = Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
INSERT INTO ArticleVariants(ArticleId, Nom, PrixVenteHT, CodeBarres, ImagePath)
VALUES ($A,$Nom,$PV,$CB,$Img);
SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("$A", v.ArticleId);
            cmd.Parameters.AddWithValue("$Nom", v.Nom);
            cmd.Parameters.AddWithValue("$PV", System.Convert.ToDouble(v.PrixVenteHT));
            cmd.Parameters.AddWithValue("$CB", (object?)v.CodeBarres ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$Img", (object?)v.ImagePath ?? DBNull.Value);
            v.Id = (int)(long)cmd.ExecuteScalar()!;
            return v;
        }

        public void DeleteVariant(int id)
        {
            using var cn = Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "DELETE FROM ArticleVariants WHERE Id=$Id;";
            cmd.Parameters.AddWithValue("$Id", id);
            cmd.ExecuteNonQuery();
        }

        // ---------- PACKS ----------
        public IEnumerable<PackItem> GetPackItems(int packArticleId)
        {
            using var cn = Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
SELECT Id, ArticlePackId, ArticleItemId, Quantite
FROM PackItems
WHERE ArticlePackId=$P
ORDER BY Id;";
            cmd.Parameters.AddWithValue("$P", packArticleId);
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                yield return new PackItem
                {
                    Id = rd.GetInt32(0),
                    ArticlePackId = rd.GetInt32(1),
                    ArticleItemId = rd.GetInt32(2),
                    Quantite = rd.GetDouble(3)   // <<< ICI : double direct (pas de decimal)
                };
            }
        }

        public PackItem InsertPackItem(PackItem p)
        {
            using var cn = Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
INSERT INTO PackItems(ArticlePackId, ArticleItemId, Quantite)
VALUES ($P,$I,$Q);
SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("$P", p.ArticlePackId);
            cmd.Parameters.AddWithValue("$I", p.ArticleItemId);
            cmd.Parameters.AddWithValue("$Q", System.Convert.ToDouble(p.Quantite)); // ok si p.Quantite est double
            p.Id = (int)(long)cmd.ExecuteScalar()!;
            return p;
        }

        public void DeletePackItem(int id)
        {
            using var cn = Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "DELETE FROM PackItems WHERE Id=$Id;";
            cmd.Parameters.AddWithValue("$Id", id);
            cmd.ExecuteNonQuery();
        }

        // ---------- IMAGES (principal 0..4) ----------
        public IEnumerable<ArticleImage> GetImages(int articleId)
        {
            using var cn = Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
SELECT Id, ArticleId, Ordre, Path
FROM ArticleImages
WHERE ArticleId=$A
ORDER BY Ordre;";
            cmd.Parameters.AddWithValue("$A", articleId);
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                yield return new ArticleImage
                {
                    Id = rd.GetInt32(0),
                    ArticleId = rd.GetInt32(1),
                    Ordre = rd.GetInt32(2),
                    Path = rd.GetString(3)
                };
            }
        }

        public void UpsertImage(int articleId, int ordre, string path)
        {
            using var cn = Open();
            using var tx = cn.BeginTransaction();

            using var cmd = cn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
INSERT INTO ArticleImages(ArticleId, Ordre, Path)
VALUES ($A,$O,$P)
ON CONFLICT(ArticleId, Ordre) DO UPDATE SET Path=$P;";
            cmd.Parameters.AddWithValue("$A", articleId);
            cmd.Parameters.AddWithValue("$O", ordre);
            cmd.Parameters.AddWithValue("$P", path);
            cmd.ExecuteNonQuery();

            tx.Commit();
        }
    }
}
