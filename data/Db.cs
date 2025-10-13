// data/Db.cs
using System.IO;
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

            var cs = new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared
            }.ToString();

            var cn = new SqliteConnection(cs);
            cn.Open();

            // Log d’ouverture (chemin exact de la DB)
            Logger.Info($"DB OPEN -> {dbPath}");

            // PRAGMA usuels
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA foreign_keys = ON;"; cmd.ExecuteNonQuery();
            }
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA journal_mode = WAL;"; cmd.ExecuteNonQuery();
            }
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA synchronous = NORMAL;"; cmd.ExecuteNonQuery();
            }

            return cn;
        }
    }
}
