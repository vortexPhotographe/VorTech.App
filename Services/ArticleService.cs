using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using VorTech.App.Models;

namespace VorTech.App.Services
{
    public static class ArticleService
    {
        public static List<Article> GetAll()
        {
            var list = new List<Article>();
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"SELECT Id, Sku, Name, PriceHT, Stock FROM Articles ORDER BY Id DESC;";
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                list.Add(new Article
                {
                    Id      = rd.GetInt32(0),
                    Sku     = rd.IsDBNull(1) ? "" : rd.GetString(1),
                    Name    = rd.IsDBNull(2) ? "" : rd.GetString(2),
                    PriceHT = rd.IsDBNull(3) ? 0.0 : rd.GetDouble(3),
                    Stock   = rd.IsDBNull(4) ? 0.0 : rd.GetDouble(4)
                });
            }
            return list;
        }

        public static Article Save(Article a)
        {
            using var cn = Db.Open();

            if (a.Id == 0)
            {
                using var cmd = cn.CreateCommand();
                cmd.CommandText = @"
INSERT INTO Articles (Sku, Name, PriceHT, Stock)
VALUES ($Sku, $Name, $PriceHT, $Stock);
SELECT last_insert_rowid();
";
                cmd.Parameters.AddWithValue("$Sku",    (object?)a.Sku ?? "");
                cmd.Parameters.AddWithValue("$Name",   (object?)a.Name ?? "");
                cmd.Parameters.AddWithValue("$PriceHT", a.PriceHT);
                cmd.Parameters.AddWithValue("$Stock",   a.Stock);
                a.Id = (int)(long)cmd.ExecuteScalar()!;
            }
            else
            {
                using var cmd = cn.CreateCommand();
                cmd.CommandText = @"
UPDATE Articles
SET Sku=$Sku, Name=$Name, PriceHT=$PriceHT, Stock=$Stock
WHERE Id=$Id;";
                cmd.Parameters.AddWithValue("$Sku",    (object?)a.Sku ?? "");
                cmd.Parameters.AddWithValue("$Name",   (object?)a.Name ?? "");
                cmd.Parameters.AddWithValue("$PriceHT", a.PriceHT);
                cmd.Parameters.AddWithValue("$Stock",   a.Stock);
                cmd.Parameters.AddWithValue("$Id",      a.Id);
                cmd.ExecuteNonQuery();
            }

            return a;
        }

        public static void Delete(int id)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "DELETE FROM Articles WHERE Id=$Id;";
            cmd.Parameters.AddWithValue("$Id", id);
            cmd.ExecuteNonQuery();
        }
    }
}
