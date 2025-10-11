using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using VorTech.App.Models;

namespace VorTech.App.Services
{
    public class SettingsCatalogService
    {
        public SettingsCatalogService() => EnsureSchema();

        private void EnsureSchema()
        {
            using var cn = Db.Open();

            // CotisationTypes
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS CotisationTypes(
  Id          INTEGER PRIMARY KEY AUTOINCREMENT,
  Name        TEXT NOT NULL,
  Liberatoire INTEGER DEFAULT 0,
  Rate        REAL DEFAULT 0,
  Notes       TEXT NULL
);";
                cmd.ExecuteNonQuery();
            }

            // TvaRates
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS TvaRates(
  Id        INTEGER PRIMARY KEY AUTOINCREMENT,
  Name      TEXT NOT NULL,
  Rate      REAL NOT NULL,
  IsDefault INTEGER DEFAULT 0,
  Notes     TEXT NULL
);";
                cmd.ExecuteNonQuery();
            }

            // Seed TVA si vide
            using (var check = cn.CreateCommand())
            {
                check.CommandText = "SELECT COUNT(*) FROM TvaRates;";
                var count = Convert.ToInt32(check.ExecuteScalar() ?? 0);
                if (count == 0)
                {
                    void ins(string name, double rate, int isDefault, string? notes = null)
                    {
                        using var insCmd = cn.CreateCommand();
                        insCmd.CommandText = "INSERT INTO TvaRates(Name, Rate, IsDefault, Notes) VALUES($n,$r,$d,$o);";
                        insCmd.Parameters.AddWithValue("$n", name);
                        insCmd.Parameters.AddWithValue("$r", rate);
                        insCmd.Parameters.AddWithValue("$d", isDefault);
                        insCmd.Parameters.AddWithValue("$o", (object?)notes ?? DBNull.Value);
                        insCmd.ExecuteNonQuery();
                    }
                    ins("0%", 0.0, 1, "Micro par défaut");
                    ins("Taux normal", 20.0, 0, null);
                    ins("Taux intermédiaire", 10.0, 0, null);
                    ins("Taux réduit", 5.5, 0, null);
                    ins("Taux particulier", 2.1, 0, null);
                }
            }
        }

        public List<CotisationType> GetCotisationTypes()
        {
            var list = new List<CotisationType>();
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, Liberatoire, Rate, Notes FROM CotisationTypes ORDER BY Name;";
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                list.Add(new CotisationType
                {
                    Id = rd.GetInt32(0),
                    Name = rd.IsDBNull(1) ? "" : rd.GetString(1),
                    Liberatoire = !rd.IsDBNull(2) && rd.GetInt32(2) != 0,
                    Rate = rd.IsDBNull(3) ? 0 : rd.GetDouble(3),
                    Notes = rd.IsDBNull(4) ? null : rd.GetString(4)
                });
            }
            return list;
        }

        public List<TvaRate> GetTvaRates()
        {
            var list = new List<TvaRate>();
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, Rate, IsDefault, Notes FROM TvaRates ORDER BY Rate DESC;";
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                list.Add(new TvaRate
                {
                    Id = rd.GetInt32(0),
                    Name = rd.IsDBNull(1) ? "" : rd.GetString(1),
                    Rate = rd.IsDBNull(2) ? 0 : rd.GetDouble(2),
                    IsDefault = !rd.IsDBNull(3) && rd.GetInt32(3) != 0,
                    Notes = rd.IsDBNull(4) ? null : rd.GetString(4)
                });
            }
            return list;
        }
    }
}
