using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using VorTech.App.Models;
using VorTech.App;

namespace VorTech.App.Services
{
    public class ClientService
    {
		 // Constructeur : garantit le schema table Clients (idempotent)
        public ClientService()
        {
            EnsureSchema();
        }

        // Ajoute les colonnes manquantes si la base a ete creee avant
        private void EnsureSchema()
        {
            using var cn = Db.Open();

            // 1) Cree la table si absente, au schema cible
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS Clients(
    Id      INTEGER PRIMARY KEY AUTOINCREMENT,
    Name    TEXT NOT NULL,
    Address TEXT NULL,
    Email   TEXT NULL,
    Phone   TEXT NULL,
    Siret   TEXT NULL,
    Notes   TEXT NULL
);";
                cmd.ExecuteNonQuery();
            }

            // 2) Liste des colonnes existantes
            var existing = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA table_info(Clients);";
                using var rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    // PRAGMA table_info: 1 = nom de colonne
                    existing.Add(rd.GetString(1));
                }
            }

            // 3) Ajoute les colonnes manquantes (idempotent)
            void AddColumnIfMissing(string name, string sqlType)
            {
                if (!existing.Contains(name))
                {
                    using var alter = cn.CreateCommand();
                    alter.CommandText = $"ALTER TABLE Clients ADD COLUMN {name} {sqlType};";
                    alter.ExecuteNonQuery();
                }
            }

            AddColumnIfMissing("Name",    "TEXT NOT NULL DEFAULT ''");
            AddColumnIfMissing("Address", "TEXT NULL");
            AddColumnIfMissing("Email",   "TEXT NULL");
            AddColumnIfMissing("Phone",   "TEXT NULL");
            AddColumnIfMissing("Siret",   "TEXT NULL");
            AddColumnIfMissing("Notes",   "TEXT NULL");

            // 4) Index (idempotent)
            using (var idx = cn.CreateCommand())
            {
                idx.CommandText = "CREATE INDEX IF NOT EXISTS IX_Clients_Name ON Clients(Name);";
                idx.ExecuteNonQuery();
            }
        }
		
        public List<Client> GetAll()
        {
            var list = new List<Client>();
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, Address, Email, Phone, Siret, Notes FROM Clients ORDER BY Name";
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                list.Add(new Client
                {
                    Id      = rd.GetInt32(0),
                    Name    = rd.GetString(1),
                    Address = rd.IsDBNull(2) ? null : rd.GetString(2),
                    Email   = rd.IsDBNull(3) ? null : rd.GetString(3),
                    Phone   = rd.IsDBNull(4) ? null : rd.GetString(4),
                    Siret   = rd.IsDBNull(5) ? null : rd.GetString(5),
                    Notes   = rd.IsDBNull(6) ? null : rd.GetString(6),
                });
            }
            return list;
        }

        public void Save(Client c)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();

            if (c.Id == 0)
            {
                cmd.CommandText = @"
INSERT INTO Clients(Name, Address, Email, Phone, Siret, Notes)
VALUES ($n,$a,$e,$p,$s,$no);
SELECT last_insert_rowid();";
                cmd.Parameters.AddWithValue("$n",  c.Name);
                cmd.Parameters.AddWithValue("$a",  (object?)c.Address ?? (object)System.DBNull.Value);
                cmd.Parameters.AddWithValue("$e",  (object?)c.Email   ?? (object)System.DBNull.Value);
                cmd.Parameters.AddWithValue("$p",  (object?)c.Phone   ?? (object)System.DBNull.Value);
                cmd.Parameters.AddWithValue("$s",  (object?)c.Siret   ?? (object)System.DBNull.Value);
                cmd.Parameters.AddWithValue("$no", (object?)c.Notes   ?? (object)System.DBNull.Value);

                var id = (long)cmd.ExecuteScalar()!;
                c.Id = (int)id;
            }
            else
            {
                cmd.CommandText = @"
UPDATE Clients
SET Name=$n, Address=$a, Email=$e, Phone=$p, Siret=$s, Notes=$no
WHERE Id=$id";
                cmd.Parameters.AddWithValue("$id", c.Id);
                cmd.Parameters.AddWithValue("$n",  c.Name);
                cmd.Parameters.AddWithValue("$a",  (object?)c.Address ?? (object)System.DBNull.Value);
                cmd.Parameters.AddWithValue("$e",  (object?)c.Email   ?? (object)System.DBNull.Value);
                cmd.Parameters.AddWithValue("$p",  (object?)c.Phone   ?? (object)System.DBNull.Value);
                cmd.Parameters.AddWithValue("$s",  (object?)c.Siret   ?? (object)System.DBNull.Value);
                cmd.Parameters.AddWithValue("$no", (object?)c.Notes   ?? (object)System.DBNull.Value);

                cmd.ExecuteNonQuery();
            }
        }

        public void Delete(int id)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "DELETE FROM Clients WHERE Id=$id";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }
    }
}
