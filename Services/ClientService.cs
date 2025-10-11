using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using VorTech.App;
using VorTech.App.Models;

namespace VorTech.App.Services
{
    public class ClientService
    {
        public ClientService()
        {
            // Migration idempotente : crée la table et ajoute les colonnes manquantes
            EnsureSchema();
        }

        private void EnsureSchema()
        {
            using var cn = Db.Open();

            // 1) Schéma cible (création si absent)
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS Clients(
    Id         INTEGER PRIMARY KEY AUTOINCREMENT,
    Nom        TEXT NOT NULL DEFAULT '',
    Prenom     TEXT NOT NULL DEFAULT '',
    Societe    TEXT NULL,
    Siret      TEXT NULL,
    Adresse    TEXT NULL,
    CodePostal TEXT NULL,
    Ville      TEXT NULL,
    Email      TEXT NULL,
    Telephone  TEXT NULL,
    NomTeam    TEXT NULL,
    Notes      TEXT NULL,
    Name       TEXT NOT NULL DEFAULT '' -- compat ancien code
);";
                cmd.ExecuteNonQuery();
            }

            // 2) Récupère les colonnes existantes
            var existing = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA table_info(Clients);";
                using var rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    existing.Add(rd.GetString(1));
                }
            }

            // 3) Ajoute les colonnes manquantes (idempotent)
            void AddCol(string name, string typeSql)
            {
                if (!existing.Contains(name))
                {
                    using var alter = cn.CreateCommand();
                    alter.CommandText = $"ALTER TABLE Clients ADD COLUMN {name} {typeSql};";
                    alter.ExecuteNonQuery();
                }
            }

            AddCol("Nom", "TEXT NOT NULL DEFAULT ''");
            AddCol("Prenom", "TEXT NOT NULL DEFAULT ''");
            AddCol("Societe", "TEXT NULL");
            AddCol("Siret", "TEXT NULL");
            AddCol("Adresse", "TEXT NULL");
            AddCol("CodePostal", "TEXT NULL");
            AddCol("Ville", "TEXT NULL");
            AddCol("Email", "TEXT NULL");
            AddCol("Telephone", "TEXT NULL");
            AddCol("NomTeam", "TEXT NULL");
            AddCol("Notes", "TEXT NULL");
            AddCol("Name", "TEXT NOT NULL DEFAULT ''");

            // 4) Index utile
            using (var idx = cn.CreateCommand())
            {
                idx.CommandText = "CREATE INDEX IF NOT EXISTS IX_Clients_Nom_Prenom ON Clients(Nom, Prenom);";
                idx.ExecuteNonQuery();
            }
        }

        public List<Client> GetAll()
        {
            var list = new List<Client>();
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
SELECT Id, Nom, Prenom, Societe, Siret, Adresse, CodePostal, Ville, Email, Telephone, NomTeam, Notes
FROM Clients
ORDER BY Nom, Prenom;";
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                var c = new Client
                {
                    Id         = rd.GetInt32(0),
                    Nom        = rd.IsDBNull(1)  ? ""   : rd.GetString(1),
                    Prenom     = rd.IsDBNull(2)  ? ""   : rd.GetString(2),
                    Societe    = rd.IsDBNull(3)  ? null : rd.GetString(3),
                    Siret      = rd.IsDBNull(4)  ? null : rd.GetString(4),
                    Adresse    = rd.IsDBNull(5)  ? null : rd.GetString(5),
                    CodePostal = rd.IsDBNull(6)  ? null : rd.GetString(6),
                    Ville      = rd.IsDBNull(7)  ? null : rd.GetString(7),
                    Email      = rd.IsDBNull(8)  ? null : rd.GetString(8),
                    Telephone  = rd.IsDBNull(9)  ? null : rd.GetString(9),
                    NomTeam    = rd.IsDBNull(10) ? null : rd.GetString(10),
                    Notes      = rd.IsDBNull(11) ? null : rd.GetString(11)
                };
                list.Add(c);
            }
            return list;
        }

        public int Add(Client c)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
INSERT INTO Clients
(Nom, Prenom, Societe, Siret, Adresse, CodePostal, Ville, Email, Telephone, NomTeam, Notes, Name)
VALUES
($nom, $prenom, $societe, $siret, $adresse, $codePostal, $ville, $email, $telephone, $nomTeam, $notes, $name);
SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("$nom",        c.Nom ?? "");
            cmd.Parameters.AddWithValue("$prenom",     c.Prenom ?? "");
            cmd.Parameters.AddWithValue("$societe",    (object?)c.Societe    ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue("$siret",      (object?)c.Siret      ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue("$adresse",    (object?)c.Adresse    ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue("$codePostal", (object?)c.CodePostal ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue("$ville",      (object?)c.Ville      ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue("$email",      (object?)c.Email      ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue("$telephone",  (object?)c.Telephone  ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue("$nomTeam",    (object?)c.NomTeam    ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue("$notes",      (object?)c.Notes      ?? System.DBNull.Value);

            var legacyName = ((c.Prenom ?? "").Trim() + " " + (c.Nom ?? "").Trim()).Trim();
            cmd.Parameters.AddWithValue("$name", legacyName);

            var scalar = cmd.ExecuteScalar();
            long id = scalar is long l ? l : System.Convert.ToInt64(scalar ?? 0);
            return (int)id;
        }

        public void Update(Client c)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
UPDATE Clients
SET Nom=$nom, Prenom=$prenom, Societe=$societe, Siret=$siret,
    Adresse=$adresse, CodePostal=$codePostal, Ville=$ville,
    Email=$email, Telephone=$telephone, NomTeam=$nomTeam, Notes=$notes,
    Name=$name
WHERE Id=$id;";
            cmd.Parameters.AddWithValue("$id",         c.Id);
            cmd.Parameters.AddWithValue("$nom",        c.Nom ?? "");
            cmd.Parameters.AddWithValue("$prenom",     c.Prenom ?? "");
            cmd.Parameters.AddWithValue("$societe",    (object?)c.Societe    ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue("$siret",      (object?)c.Siret      ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue("$adresse",    (object?)c.Adresse    ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue("$codePostal", (object?)c.CodePostal ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue("$ville",      (object?)c.Ville      ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue("$email",      (object?)c.Email      ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue("$telephone",  (object?)c.Telephone  ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue("$nomTeam",    (object?)c.NomTeam    ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue("$notes",      (object?)c.Notes      ?? System.DBNull.Value);

            var legacyName = ((c.Prenom ?? "").Trim() + " " + (c.Nom ?? "").Trim()).Trim();
            cmd.Parameters.AddWithValue("$name", legacyName);

            cmd.ExecuteNonQuery();
        }

        public void Save(Client c)
        {
            if (c == null) return;

            if (c.Id == 0)
                c.Id = Add(c);
            else
                Update(c);
        }

        public void Delete(int id)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "DELETE FROM Clients WHERE Id=$id;";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }
    }
}
