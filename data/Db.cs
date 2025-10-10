using System.IO;
using Microsoft.Data.Sqlite;

namespace VorTech.App
{
    public static class Db
    {
        public static string ConnectionString => new SqliteConnectionStringBuilder
        {
            DataSource = Paths.DbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        public static void Init()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Paths.DbPath)!);

            using var cn = new SqliteConnection(ConnectionString);
            cn.Open();

            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
PRAGMA journal_mode = WAL;
PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS Clients(
  Id        INTEGER PRIMARY KEY AUTOINCREMENT,
  Name      TEXT    NOT NULL,
  Address   TEXT,
  Email     TEXT,
  Phone     TEXT,
  CreatedAt TEXT    NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS Articles(
  Id        INTEGER PRIMARY KEY AUTOINCREMENT,
  Sku       TEXT UNIQUE,
  Name      TEXT    NOT NULL,
  PriceHT   REAL    NOT NULL DEFAULT 0,
  Stock     REAL    NOT NULL DEFAULT 0,
  CreatedAt TEXT    NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS Documents(
  Id        INTEGER PRIMARY KEY AUTOINCREMENT,
  Type      TEXT    NOT NULL,            -- 'DEV' ou 'FAC'
  Number    TEXT    NOT NULL UNIQUE,
  ClientId  INTEGER NOT NULL REFERENCES Clients(Id) ON DELETE RESTRICT,
  Date      TEXT    NOT NULL,
  Total     REAL    NOT NULL DEFAULT 0,
  Status    TEXT    NOT NULL DEFAULT 'A_regler'
);

CREATE TABLE IF NOT EXISTS DocumentLines(
  Id         INTEGER PRIMARY KEY AUTOINCREMENT,
  DocumentId INTEGER NOT NULL REFERENCES Documents(Id) ON DELETE CASCADE,
  Designation TEXT   NOT NULL,
  Qty        REAL    NOT NULL DEFAULT 1,
  Price      REAL    NOT NULL DEFAULT 0,
  LineTotal  AS (Qty * Price) STORED
);

CREATE INDEX IF NOT EXISTS IX_Documents_Client ON Documents(ClientId);
";
            cmd.ExecuteNonQuery();
        }

        public static SqliteConnection Open()
        {
            var cn = new SqliteConnection(ConnectionString);
            cn.Open();
            return cn;
        }
    }
}
