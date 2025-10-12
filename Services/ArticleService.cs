using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using VorTech.App.Models;

namespace VorTech.App.Services
{
    public static class ArticleService
    {
        static ArticleService() => EnsureSchema();

        private static string ConnString => $"Data Source={Paths.DbPath}";

        private static void EnsureSchema()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Paths.DbPath)!);
            using var cn = new SqliteConnection(ConnString);
            cn.Open();

            var sql = @"
CREATE TABLE IF NOT EXISTS Articles(
  Id            INTEGER PRIMARY KEY AUTOINCREMENT,
  Reference     TEXT NOT NULL DEFAULT '',
  Libelle       TEXT NOT NULL DEFAULT '',
  Type          INTEGER NOT NULL DEFAULT 0,
  CategoryId    INTEGER NULL,
  PrixAchatHT   REAL NOT NULL DEFAULT 0,
  PrixVenteHT   REAL NOT NULL DEFAULT 0,
  StockActuel   INTEGER NOT NULL DEFAULT 0,
  SeuilAlerte   INTEGER NOT NULL DEFAULT 0,
  Poids_g       REAL NOT NULL DEFAULT 0,
  Actif         INTEGER NOT NULL DEFAULT 1,
  CodeBarres    TEXT NOT NULL DEFAULT '',
  Description   TEXT NULL,
  DateMaj       TEXT NOT NULL
);
";
            using var cmd = cn.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        public static List<Article> GetAll()
        {
            var list = new List<Article>();
            using var cn = new SqliteConnection(ConnString);
            cn.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"SELECT Id,Reference,Libelle,Type,CategoryId,PrixAchatHT,PrixVenteHT,
                                       StockActuel,SeuilAlerte,Poids_g,Actif,CodeBarres,Description,DateMaj
                                FROM Articles
                                ORDER BY Libelle COLLATE NOCASE";
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                list.Add(new Article {
                    Id           = rd.GetInt32(0),
                    Reference    = rd.GetString(1),
                    Libelle      = rd.GetString(2),
                    Type         = (ArticleType)rd.GetInt32(3),
                    CategoryId   = rd.IsDBNull(4) ? null : rd.GetInt32(4),
                    PrixAchatHT  = rd.GetDouble(5),
                    PrixVenteHT  = rd.GetDouble(6),
                    StockActuel  = rd.GetInt32(7),
                    SeuilAlerte  = rd.GetInt32(8),
                    Poids_g      = rd.GetDouble(9),
                    Actif        = rd.GetInt32(10) == 1,
                    CodeBarres   = rd.GetString(11),
                    Description  = rd.IsDBNull(12) ? null : rd.GetString(12),
                    DateMaj      = DateTime.Parse(rd.GetString(13), CultureInfo.InvariantCulture)
                });
            }
            return list;
        }

        public static Article? GetById(int id)
        {
            using var cn = new SqliteConnection(ConnString);
            cn.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"SELECT Id,Reference,Libelle,Type,CategoryId,PrixAchatHT,PrixVenteHT,
                                       StockActuel,SeuilAlerte,Poids_g,Actif,CodeBarres,Description,DateMaj
                                FROM Articles WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", id);
            using var rd = cmd.ExecuteReader();
            if (!rd.Read()) return null;

            return new Article {
                Id           = rd.GetInt32(0),
                Reference    = rd.GetString(1),
                Libelle      = rd.GetString(2),
                Type         = (ArticleType)rd.GetInt32(3),
                CategoryId   = rd.IsDBNull(4) ? null : rd.GetInt32(4),
                PrixAchatHT  = rd.GetDouble(5),
                PrixVenteHT  = rd.GetDouble(6),
                StockActuel  = rd.GetInt32(7),
                SeuilAlerte  = rd.GetInt32(8),
                Poids_g      = rd.GetDouble(9),
                Actif        = rd.GetInt32(10) == 1,
                CodeBarres   = rd.GetString(11),
                Description  = rd.IsDBNull(12) ? null : rd.GetString(12),
                DateMaj      = DateTime.Parse(rd.GetString(13), CultureInfo.InvariantCulture)
            };
        }

        public static int Save(Article a)
        {
            a.DateMaj = DateTime.UtcNow;
            using var cn = new SqliteConnection(ConnString);
            cn.Open();
            using var cmd = cn.CreateCommand();

            if (a.Id == 0)
            {
                cmd.CommandText = @"
INSERT INTO Articles(Reference,Libelle,Type,CategoryId,PrixAchatHT,PrixVenteHT,StockActuel,SeuilAlerte,Poids_g,Actif,CodeBarres,Description,DateMaj)
VALUES(@ref,@lib,@typ,@cat,@pa,@pv,@stock,@seuil,@poids,@actif,@bar,@desc,@maj);
SELECT last_insert_rowid();";
            }
            else
            {
                cmd.CommandText = @"
UPDATE Articles SET Reference=@ref,Libelle=@lib,Type=@typ,CategoryId=@cat,
    PrixAchatHT=@pa,PrixVenteHT=@pv,StockActuel=@stock,SeuilAlerte=@seuil,
    Poids_g=@poids,Actif=@actif,CodeBarres=@bar,Description=@desc,DateMaj=@maj
WHERE Id=@id;
SELECT @id;";
                cmd.Parameters.AddWithValue("@id", a.Id);
            }

            cmd.Parameters.AddWithValue("@ref",  a.Reference ?? "");
            cmd.Parameters.AddWithValue("@lib",  a.Libelle ?? "");
            cmd.Parameters.AddWithValue("@typ",  (int)a.Type);
            cmd.Parameters.AddWithValue("@cat",  (object?)a.CategoryId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@pa",   a.PrixAchatHT);
            cmd.Parameters.AddWithValue("@pv",   a.PrixVenteHT);
            cmd.Parameters.AddWithValue("@stock",a.StockActuel);
            cmd.Parameters.AddWithValue("@seuil",a.SeuilAlerte);
            cmd.Parameters.AddWithValue("@poids",a.Poids_g);
            cmd.Parameters.AddWithValue("@actif",a.Actif ? 1 : 0);
            cmd.Parameters.AddWithValue("@bar",  a.CodeBarres ?? "");
            cmd.Parameters.AddWithValue("@desc", (object?)a.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@maj",  a.DateMaj.ToString("o", CultureInfo.InvariantCulture));

            var newId = Convert.ToInt32(cmd.ExecuteScalar());
            if (a.Id == 0) a.Id = newId;
            return a.Id;
        }

        public static void Delete(int id)
        {
            using var cn = new SqliteConnection(ConnString);
            cn.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "DELETE FROM Articles WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
    }
}
