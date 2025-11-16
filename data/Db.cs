// data/Db.cs
using System.IO;
using System;
using System.Data;
using Microsoft.Data.Sqlite;
using VorTech.App.Services; // <- pour Logger

namespace VorTech.App
{
    public static class Db
    {
        public static SqliteConnection Open()
        {
            // même fichier que le reste de l’app (Clients)
            var dbPath = Paths.DbPath;

            var dir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var csb = new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared,

                // NEW: délai d’attente par défaut (équivalent busy_timeout côté provider)
                DefaultTimeout = 5 // secondes
            };

            var cn = new SqliteConnection(csb.ToString());
            cn.Open();

            // Log d’ouverture (chemin exact de la DB)
            Logger.Info($"DB OPEN -> {dbPath}");

            // PRAGMA usuels (on garde les tiens)
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA foreign_keys = ON;";
                cmd.ExecuteNonQuery();
            }
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA journal_mode = WAL;";
                cmd.ExecuteNonQuery();
            }
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA synchronous = NORMAL;";
                cmd.ExecuteNonQuery();
            }

            // NEW: ajoute explicitement busy_timeout pour SQLite (complémentaire à DefaultTimeout)
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA busy_timeout=5000;"; // 5000 ms
                cmd.ExecuteNonQuery();
            }

            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA busy_timeout = 5000;"; // 5s d’attente si la table est verrouillée
                cmd.ExecuteNonQuery();
            }

            return cn;
        }

        public static void AddParam(IDbCommand cmd, string name, object? value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }
    }
}
