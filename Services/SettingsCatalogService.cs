using System;
using System.Collections.Generic;
using System.Linq;
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
  Id   INTEGER PRIMARY KEY AUTOINCREMENT,
  Name TEXT    NOT NULL,
  Rate REAL    NOT NULL DEFAULT 0
);";
                cmd.ExecuteNonQuery();
            }

            // TvaRates
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS TvaRates(
  Id        INTEGER PRIMARY KEY AUTOINCREMENT,
  Name      TEXT    NOT NULL,
  Rate      REAL    NOT NULL DEFAULT 0,
  IsDefault INTEGER NOT NULL DEFAULT 0
);";
                cmd.ExecuteNonQuery();
            }

            // Categories
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS Categories(
  Id    INTEGER PRIMARY KEY AUTOINCREMENT,
  Name  TEXT NOT NULL,
  Actif INTEGER DEFAULT 1
);";
                cmd.ExecuteNonQuery();
            }

            // --- CompanyProfile : 1 seule ligne ---
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS CompanyProfile(
              Id            INTEGER PRIMARY KEY CHECK (Id=1),
              NomCommercial TEXT    NOT NULL DEFAULT '',
              Siret         TEXT    NOT NULL DEFAULT '',
              Adresse1      TEXT    NOT NULL DEFAULT '',
              Adresse2      TEXT    NOT NULL DEFAULT '',
              CodePostal    TEXT    NOT NULL DEFAULT '',
              Ville         TEXT    NOT NULL DEFAULT '',
              Pays          TEXT    NOT NULL DEFAULT '',
              Email         TEXT    NOT NULL DEFAULT '',
              Telephone     TEXT    NOT NULL DEFAULT '',
              SiteWeb       TEXT    NOT NULL DEFAULT ''
            );";
                cmd.ExecuteNonQuery();
            }
            // Seed 1 ligne si vide
            using (var check = cn.CreateCommand())
            {
                check.CommandText = "SELECT COUNT(*) FROM CompanyProfile;";
                var count = Convert.ToInt32(check.ExecuteScalar() ?? 0);
                if (count == 0)
                {
                    using var ins = cn.CreateCommand();
                    ins.CommandText = @"
INSERT INTO CompanyProfile(Id, NomCommercial, Siret, Adresse1, Adresse2, CodePostal, Ville, Pays, Email, Telephone, SiteWeb)
VALUES(1, '', '', '', '', '', '', '', '', '', '');";
                    ins.ExecuteNonQuery();
                }
            }

            // Seed TVA si vide
            using (var check = cn.CreateCommand())
            {
                check.CommandText = "SELECT COUNT(*) FROM TvaRates;";
                var count = Convert.ToInt32(check.ExecuteScalar() ?? 0);
                if (count == 0)
                {
                    void ins(string name, double rate, int isDefault)
                    {
                        using var insCmd = cn.CreateCommand();
                        insCmd.CommandText = "INSERT INTO TvaRates(Name, Rate, IsDefault) VALUES($n,$r,$d);";
                        insCmd.Parameters.AddWithValue("$n", name);
                        insCmd.Parameters.AddWithValue("$r", rate);
                        insCmd.Parameters.AddWithValue("$d", isDefault);
                        insCmd.ExecuteNonQuery();
                    }
                    ins("0%", 0.0, 1);
                    ins("Taux normal", 20.0, 0);
                    ins("Taux intermédiaire", 10.0, 0);
                    ins("Taux réduit", 5.5, 0);
                    ins("Taux particulier", 2.1, 0);
                }
            }

            // Seed Catégorie par défaut si vide
            using (var check = cn.CreateCommand())
            {
                check.CommandText = "SELECT COUNT(*) FROM Categories;";
                var count = Convert.ToInt32(check.ExecuteScalar() ?? 0);
                if (count == 0)
                {
                    using var ins = cn.CreateCommand();
                    ins.CommandText = "INSERT INTO Categories(Name, Actif) VALUES('Général',1);";
                    ins.ExecuteNonQuery();
                }
            }
        }

        public List<CotisationType> GetCotisationTypes()
        {
            var list = new List<CotisationType>();
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, Rate FROM CotisationTypes ORDER BY Name COLLATE NOCASE";
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                list.Add(new CotisationType
                {
                    Id = rd.GetInt32(0),
                    Name = rd.IsDBNull(1) ? "" : rd.GetString(1),
                    Rate = rd.IsDBNull(2) ? 0 : rd.GetDouble(2),
                });
            }
            return list;
        }

        // Alias pour coller à l'usage actuel
        public List<CotisationType> GetCotisationRates() => GetCotisationTypes();

        public List<TvaRate> GetTvaRates()
        {
            var list = new List<TvaRate>();
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, Rate, IsDefault FROM TvaRates ORDER BY Name COLLATE NOCASE";
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                list.Add(new TvaRate
                {
                    Id = rd.GetInt32(0),
                    Name = rd.IsDBNull(1) ? "" : rd.GetString(1),
                    Rate = rd.IsDBNull(2) ? 0 : rd.GetDouble(2),
                    IsDefault = !rd.IsDBNull(3) && rd.GetInt32(3) != 0,
                });
            }
            return list;
        }

        public List<Categorie> GetCategories()
        {
            var list = new List<Categorie>();
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, Actif FROM Categories ORDER BY Name;";
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                list.Add(new Categorie
                {
                    Id = rd.GetInt32(0),
                    Name = rd.IsDBNull(1) ? "" : rd.GetString(1),
                    Actif = !rd.IsDBNull(2) && rd.GetInt32(2) != 0
                });
            }
            return list;
        }

        public decimal GetRateById(int? id)
        {
            if (id == null) return 0m;
            var tva = GetTvaRates().FirstOrDefault(x => x.Id == id)?.Rate ?? 0.0;
            var cot = GetCotisationTypes().FirstOrDefault(x => x.Id == id)?.Rate ?? 0.0;
            var r = tva != 0.0 ? tva : cot;
            return (decimal)r;
        }

        // CRUD
        // TAB  Category
        public int InsertCategory(string name, bool actif)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"INSERT INTO Categories(Name, Actif) VALUES(@n,@a);
                        SELECT last_insert_rowid();";
            Db.AddParam(cmd, "@n", name ?? "");
            Db.AddParam(cmd, "@a", actif ? 1 : 0);
            return Convert.ToInt32(cmd.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
        }

        public void UpdateCategory(int id, string name, bool actif)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "UPDATE Categories SET Name=@n, Actif=@a WHERE Id=@id;";
            Db.AddParam(cmd, "@n", name ?? "");
            Db.AddParam(cmd, "@a", actif ? 1 : 0);
            Db.AddParam(cmd, "@id", id);
            cmd.ExecuteNonQuery();
        }

        public void DeleteCategory(int id)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "DELETE FROM Categories WHERE Id=@id;";
            Db.AddParam(cmd, "@id", id);
            cmd.ExecuteNonQuery();
        }

        // TAB TVA
        public int InsertTvaRate(string name, decimal rate, bool isDefault)
        {
            using var cn = Db.Open();
            using var tx = cn.BeginTransaction();

            if (isDefault)
            {
                using var clear = cn.CreateCommand();
                clear.CommandText = "UPDATE TvaRates SET IsDefault=0;";
                clear.ExecuteNonQuery();
            }

            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"INSERT INTO TvaRates(Name, Rate, IsDefault) VALUES(@n,@r,@d);
                        SELECT last_insert_rowid();";
            Db.AddParam(cmd, "@n", name ?? "");
            Db.AddParam(cmd, "@r", rate);
            Db.AddParam(cmd, "@d", isDefault ? 1 : 0);
            var id = Convert.ToInt32(cmd.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);

            tx.Commit();
            return id;
        }

        public void UpdateTvaRate(int id, string name, decimal rate, bool isDefault)
        {
            using var cn = Db.Open();
            using var tx = cn.BeginTransaction();

            if (isDefault)
            {
                using var clear = cn.CreateCommand();
                clear.CommandText = "UPDATE TvaRates SET IsDefault=0;";
                clear.ExecuteNonQuery();
            }

            using var cmd = cn.CreateCommand();
            cmd.CommandText = "UPDATE TvaRates SET Name=@n, Rate=@r, IsDefault=@d WHERE Id=@id;";
            Db.AddParam(cmd, "@n", name ?? "");
            Db.AddParam(cmd, "@r", rate);
            Db.AddParam(cmd, "@d", isDefault ? 1 : 0);
            Db.AddParam(cmd, "@id", id);
            cmd.ExecuteNonQuery();

            tx.Commit();
        }

        public void DeleteTvaRate(int id)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "DELETE FROM TvaRates WHERE Id=@id;";
            Db.AddParam(cmd, "@id", id);
            cmd.ExecuteNonQuery();
        }

        // TAB Cotisations
        public int InsertCotisationType(string name, decimal rate)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"INSERT INTO CotisationTypes(Name, Rate) VALUES(@n,@r);
                        SELECT last_insert_rowid();";
            Db.AddParam(cmd, "@n", name ?? "");
            Db.AddParam(cmd, "@r", rate);
            return Convert.ToInt32(cmd.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
        }

        public void UpdateCotisationType(int id, string name, decimal rate)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "UPDATE CotisationTypes SET Name=@n, Rate=@r WHERE Id=@id;";
            Db.AddParam(cmd, "@n", name ?? "");
            Db.AddParam(cmd, "@r", rate);
            Db.AddParam(cmd, "@id", id);
            cmd.ExecuteNonQuery();
        }

        public void DeleteCotisationType(int id)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "DELETE FROM CotisationTypes WHERE Id=@id;";
            Db.AddParam(cmd, "@id", id);
            cmd.ExecuteNonQuery();
        }

        // TAB CompanyProfile PAS DE SUPR
        public CompanyProfile GetCompanyProfile()
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
SELECT Id, NomCommercial, Siret, Adresse1, Adresse2, CodePostal, Ville, Pays, Email, Telephone, SiteWeb
FROM CompanyProfile
WHERE Id=1;";
            using var rd = cmd.ExecuteReader();
            if (rd.Read())
            {
                return new CompanyProfile
                {
                    Id = 1,
                    NomCommercial = rd["NomCommercial"]?.ToString() ?? "",
                    Siret = rd["Siret"]?.ToString() ?? "",
                    Adresse1 = rd["Adresse1"]?.ToString() ?? "",
                    Adresse2 = rd["Adresse2"]?.ToString() ?? "",
                    CodePostal = rd["CodePostal"]?.ToString() ?? "",
                    Ville = rd["Ville"]?.ToString() ?? "",
                    Pays = rd["Pays"]?.ToString() ?? "",
                    Email = rd["Email"]?.ToString() ?? "",
                    Telephone = rd["Telephone"]?.ToString() ?? "",
                    SiteWeb = rd["SiteWeb"]?.ToString() ?? ""
                };
            }
            return new CompanyProfile(); // fallback
        }

        public void SaveCompanyProfile(CompanyProfile p)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
UPDATE CompanyProfile SET
  NomCommercial=@n,
  Siret=@s,
  Adresse1=@a1,
  Adresse2=@a2,
  CodePostal=@cp,
  Ville=@v,
  Pays=@pays,
  Email=@mail,
  Telephone=@tel,
  SiteWeb=@web
WHERE Id=1;";
            Db.AddParam(cmd, "@n", p.NomCommercial ?? "");
            Db.AddParam(cmd, "@s", p.Siret ?? "");
            Db.AddParam(cmd, "@a1", p.Adresse1 ?? "");
            Db.AddParam(cmd, "@a2", p.Adresse2 ?? "");
            Db.AddParam(cmd, "@cp", p.CodePostal ?? "");
            Db.AddParam(cmd, "@v", p.Ville ?? "");
            Db.AddParam(cmd, "@pays", p.Pays ?? "");
            Db.AddParam(cmd, "@mail", p.Email ?? "");
            Db.AddParam(cmd, "@tel", p.Telephone ?? "");
            Db.AddParam(cmd, "@web", p.SiteWeb ?? "");
            cmd.ExecuteNonQuery();
        }
    }
}
