using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using Microsoft.Data.Sqlite;

namespace VorTech.App
{
    public static class Db
    {
        public static string Conn => new SqliteConnectionStringBuilder { DataSource = Paths.DbPath }.ToString();

        public static void EnsureDatabase(out string status)
        {
            bool created = !File.Exists(Paths.DbPath);
            if (created)
            {
                using var cn0 = new SqliteConnection(Conn);
                cn0.Open();
                cn0.Close();
            }
            const string schema = @"
PRAGMA foreign_keys=ON;

CREATE TABLE IF NOT EXISTS Settings(
  Key TEXT PRIMARY KEY,
  Value TEXT
);

CREATE TABLE IF NOT EXISTS Clients(
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  Name TEXT NOT NULL,
  Email TEXT,
  Phone TEXT,
  Address1 TEXT,
  Address2 TEXT,
  PostCode TEXT,
  City TEXT,
  Country TEXT
);

CREATE TABLE IF NOT EXISTS Articles(
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  Sku TEXT UNIQUE,
  Name TEXT NOT NULL,
  CostHT REAL NOT NULL DEFAULT 0,
  PriceHT REAL NOT NULL DEFAULT 0,
  StockQty REAL NOT NULL DEFAULT 0,
  MinQty REAL NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS Invoices(
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  Number TEXT UNIQUE,
  ClientId INTEGER NOT NULL,
  Date TEXT NOT NULL,
  NoteTop TEXT,
  NoteBottom TEXT,
  TotalHT REAL NOT NULL DEFAULT 0,
  RemiseEUR REAL NOT NULL DEFAULT 0,
  Status TEXT NOT NULL DEFAULT 'A_REGLE',
  DueMode TEXT NOT NULL DEFAULT 'CMD',
  AcomptePct INTEGER,
  FOREIGN KEY(ClientId) REFERENCES Clients(Id)
);

CREATE TABLE IF NOT EXISTS InvoiceLines(
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  InvoiceId INTEGER NOT NULL,
  ArticleId INTEGER,
  Designation TEXT NOT NULL,
  Qty REAL NOT NULL DEFAULT 1,
  PUHT REAL NOT NULL DEFAULT 0,
  LineTotalHT REAL NOT NULL DEFAULT 0,
  FOREIGN KEY(InvoiceId) REFERENCES Invoices(Id) ON DELETE CASCADE,
  FOREIGN KEY(ArticleId) REFERENCES Articles(Id)
);

CREATE TABLE IF NOT EXISTS PaymentMethods(
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  Name TEXT UNIQUE NOT NULL,
  FixedFee REAL NOT NULL DEFAULT 0,
  PercentFee REAL NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS Payments(
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  InvoiceId INTEGER NOT NULL,
  PaymentMethodId INTEGER,
  Label TEXT,
  Date TEXT NOT NULL,
  Amount REAL NOT NULL,
  FeeFixed REAL NOT NULL,
  FeePercent REAL NOT NULL,
  FeeAmount REAL NOT NULL,
  NetAmount REAL NOT NULL,
  FOREIGN KEY(InvoiceId) REFERENCES Invoices(Id) ON DELETE CASCADE,
  FOREIGN KEY(PaymentMethodId) REFERENCES PaymentMethods(Id)
);
";
            Exec(schema);

            if (GetSetting("Micro.Mention293B") == null)
                SetSetting("Micro.Mention293B", "true");

            if (ScalarInt("SELECT COUNT(1) FROM Clients") == 0)
                Exec("INSERT INTO Clients(Name,Email,Phone,City,Country) VALUES ('Client Démo','demo@vortech.fr','+33 700000000','Biarritz','France');");

            if (ScalarInt("SELECT COUNT(1) FROM Articles") == 0)
            {
                Exec("INSERT INTO Articles(Sku,Name,CostHT,PriceHT,StockQty,MinQty) VALUES ('UV5R-BLK','Baofeng UV-5R Noir',20,35,5,1);");
                Exec("INSERT INTO Articles(Sku,Name,CostHT,PriceHT,StockQty,MinQty) VALUES ('RT86-STD','Retevis RT86',45,79,3,1);");
            }

            if (ScalarInt("SELECT COUNT(1) FROM PaymentMethods") == 0)
            {
                Exec("INSERT INTO PaymentMethods(Name,FixedFee,PercentFee) VALUES ('Espèces',0,0);");
                Exec("INSERT INTO PaymentMethods(Name,FixedFee,PercentFee) VALUES ('CB',0.25,1.5);");
                Exec("INSERT INTO PaymentMethods(Name,FixedFee,PercentFee) VALUES ('Virement',0,0);");
                Exec("INSERT INTO PaymentMethods(Name,FixedFee,PercentFee) VALUES ('PayPal',0.35,2.9);");
            }

            status = created ? "créée et initialisée" : "ouverte";
        }

        public static void Exec(string sql)
        {
            using var cn = new SqliteConnection(Conn); cn.Open();
            using var cmd = cn.CreateCommand(); cmd.CommandText = sql; cmd.ExecuteNonQuery();
        }
        public static int ScalarInt(string sql)
        {
            using var cn = new SqliteConnection(Conn); cn.Open();
            using var cmd = cn.CreateCommand(); cmd.CommandText = sql;
            var o = cmd.ExecuteScalar();
            return o == null || o is DBNull ? 0 : Convert.ToInt32(o);
        }

        public static string? GetSetting(string key)
        {
            using var cn = new SqliteConnection(Conn); cn.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT Value FROM Settings WHERE Key=$k"; cmd.Parameters.AddWithValue("$k", key);
            var o = cmd.ExecuteScalar(); return o == null || o is DBNull ? null : Convert.ToString(o);
        }
        public static void SetSetting(string key, string val)
        {
            using var cn = new SqliteConnection(Conn); cn.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "INSERT INTO Settings(Key,Value) VALUES ($k,$v) ON CONFLICT(Key) DO UPDATE SET Value=$v;";
            cmd.Parameters.AddWithValue("$k", key); cmd.Parameters.AddWithValue("$v", val);
            cmd.ExecuteNonQuery();
        }

        public static List<Client> ClientsAll(string? filter=null)
        {
            var list = new List<Client>();
            using var cn = new SqliteConnection(Conn); cn.Open();
            using var cmd = cn.CreateCommand();
            if (string.IsNullOrWhiteSpace(filter))
                cmd.CommandText = "SELECT Id,Name,Email,Phone,Address1,Address2,PostCode,City,Country FROM Clients ORDER BY Name;";
            else {
                cmd.CommandText = "SELECT Id,Name,Email,Phone,Address1,Address2,PostCode,City,Country FROM Clients WHERE lower(Name) LIKE $q OR lower(Email) LIKE $q ORDER BY Name;";
                cmd.Parameters.AddWithValue("$q", "%"+filter.ToLower()+"%");
            }
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
                list.Add(new Client {
                    Id = rd.GetInt32(0), Name = rd.GetString(1),
                    Email = rd.IsDBNull(2) ? "" : rd.GetString(2),
                    Phone = rd.IsDBNull(3) ? "" : rd.GetString(3),
                    Address1 = rd.IsDBNull(4) ? "" : rd.GetString(4),
                    Address2 = rd.IsDBNull(5) ? "" : rd.GetString(5),
                    PostCode = rd.IsDBNull(6) ? "" : rd.GetString(6),
                    City = rd.IsDBNull(7) ? "" : rd.GetString(7),
                    Country = rd.IsDBNull(8) ? "" : rd.GetString(8),
                });
            return list;
        }
        public static int ClientUpsert(Client c)
        {
            using var cn = new SqliteConnection(Conn); cn.Open();
            using var cmd = cn.CreateCommand();
            if (c.Id == 0)
                cmd.CommandText = @"INSERT INTO Clients(Name,Email,Phone,Address1,Address2,PostCode,City,Country)
                                    VALUES($n,$e,$p,$a1,$a2,$pc,$ci,$co); SELECT last_insert_rowid();";
            else {
                cmd.CommandText = @"UPDATE Clients SET Name=$n,Email=$e,Phone=$p,Address1=$a1,Address2=$a2,PostCode=$pc,City=$ci,Country=$co WHERE Id=$id;
                                    SELECT $id;"; cmd.Parameters.AddWithValue("$id", c.Id);
            }
            cmd.Parameters.AddWithValue("$n", c.Name);
            cmd.Parameters.AddWithValue("$e", string.IsNullOrWhiteSpace(c.Email)? DBNull.Value : c.Email);
            cmd.Parameters.AddWithValue("$p", string.IsNullOrWhiteSpace(c.Phone)? DBNull.Value : c.Phone);
            cmd.Parameters.AddWithValue("$a1", string.IsNullOrWhiteSpace(c.Address1)? DBNull.Value : c.Address1);
            cmd.Parameters.AddWithValue("$a2", string.IsNullOrWhiteSpace(c.Address2)? DBNull.Value : c.Address2);
            cmd.Parameters.AddWithValue("$pc", string.IsNullOrWhiteSpace(c.PostCode)? DBNull.Value : c.PostCode);
            cmd.Parameters.AddWithValue("$ci", string.IsNullOrWhiteSpace(c.City)? DBNull.Value : c.City);
            cmd.Parameters.AddWithValue("$co", string.IsNullOrWhiteSpace(c.Country)? DBNull.Value : c.Country);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
        public static void ClientDelete(int id)
        {
            using var cn = new SqliteConnection(Conn); cn.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "DELETE FROM Clients WHERE Id=$id;"; cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }

        public static List<Article> ArticlesAll(string? filter=null)
        {
            var list = new List<Article>();
            using var cn = new SqliteConnection(Conn); cn.Open();
            using var cmd = cn.CreateCommand();
            if (string.IsNullOrWhiteSpace(filter))
                cmd.CommandText = "SELECT Id,Sku,Name,CostHT,PriceHT,StockQty,MinQty FROM Articles ORDER BY Name;";
            else {
                cmd.CommandText = "SELECT Id,Sku,Name,CostHT,PriceHT,StockQty,MinQty FROM Articles WHERE lower(Name) LIKE $q OR lower(Sku) LIKE $q ORDER BY Name;";
                cmd.Parameters.AddWithValue("$q", "%"+filter.ToLower()+"%");
            }
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
                list.Add(new Article {
                    Id = rd.GetInt32(0), Sku = rd.IsDBNull(1) ? "" : rd.GetString(1),
                    Name = rd.GetString(2), CostHT = rd.GetDouble(3), PriceHT = rd.GetDouble(4),
                    StockQty = rd.GetDouble(5), MinQty = rd.GetDouble(6)
                });
            return list;
        }
        public static int ArticleUpsert(Article a)
        {
            using var cn = new SqliteConnection(Conn); cn.Open();
            using var cmd = cn.CreateCommand();
            if (a.Id == 0)
                cmd.CommandText = @"INSERT INTO Articles(Sku,Name,CostHT,PriceHT,StockQty,MinQty)
                                    VALUES($s,$n,$c,$p,$q,$m); SELECT last_insert_rowid();";
            else {
                cmd.CommandText = @"UPDATE Articles SET Sku=$s,Name=$n,CostHT=$c,PriceHT=$p,StockQty=$q,MinQty=$m WHERE Id=$id; SELECT $id;";
                cmd.Parameters.AddWithValue("$id", a.Id);
            }
            cmd.Parameters.AddWithValue("$s", string.IsNullOrWhiteSpace(a.Sku)? DBNull.Value : a.Sku);
            cmd.Parameters.AddWithValue("$n", a.Name);
            cmd.Parameters.AddWithValue("$c", a.CostHT);
            cmd.Parameters.AddWithValue("$p", a.PriceHT);
            cmd.Parameters.AddWithValue("$q", a.StockQty);
            cmd.Parameters.AddWithValue("$m", a.MinQty);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
        public static void ArticleDelete(int id)
        {
            using var cn = new SqliteConnection(Conn); cn.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "DELETE FROM Articles WHERE Id=$id;"; cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }

        public static List<PaymentMethod> PaymentMethodsAll()
        {
            var list = new List<PaymentMethod>();
            using var cn = new SqliteConnection(Conn); cn.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT Id,Name,FixedFee,PercentFee FROM PaymentMethods ORDER BY Name;";
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
                list.Add(new PaymentMethod {
                    Id = rd.GetInt32(0), Name = rd.GetString(1),
                    FixedFee = rd.GetDouble(2), PercentFee = rd.GetDouble(3)
                });
            return list;
        }
        public static int PaymentMethodUpsert(PaymentMethod p)
        {
            using var cn = new SqliteConnection(Conn); cn.Open();
            using var cmd = cn.CreateCommand();
            if (p.Id == 0)
                cmd.CommandText = @"INSERT INTO PaymentMethods(Name,FixedFee,PercentFee)
                                    VALUES($n,$f,$pc); SELECT last_insert_rowid();";
            else {
                cmd.CommandText = @"UPDATE PaymentMethods SET Name=$n,FixedFee=$f,PercentFee=$pc WHERE Id=$id; SELECT $id;";
                cmd.Parameters.AddWithValue("$id", p.Id);
            }
            cmd.Parameters.AddWithValue("$n", p.Name);
            cmd.Parameters.AddWithValue("$f", p.FixedFee);
            cmd.Parameters.AddWithValue("$pc", p.PercentFee);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
        public static void PaymentMethodDelete(int id)
        {
            using var cn = new SqliteConnection(Conn); cn.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "DELETE FROM PaymentMethods WHERE Id=$id;"; cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }

        public static string NewInvoiceNumber()
        {
            var y = DateTime.Today.Year;
            var count = ScalarInt($"SELECT COUNT(1) FROM Invoices WHERE substr(Number,1,3)='FAC' AND substr(Number,5,4)='{y}'");
            return $"FAC-{y}-{(count+1):0000}";
        }
    }

    public class Client
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Address1 { get; set; } = "";
        public string Address2 { get; set; } = "";
        public string PostCode { get; set; } = "";
        public string City { get; set; } = "";
        public string Country { get; set; } = "";
        public override string ToString() => Name;
    }
    public class Article
    {
        public int Id { get; set; }
        public string Sku { get; set; } = "";
        public string Name { get; set; } = "";
        public double CostHT { get; set; }
        public double PriceHT { get; set; }
        public double StockQty { get; set; }
        public double MinQty { get; set; }
        public override string ToString() => Name;
    }
    public class PaymentMethod
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public double FixedFee { get; set; }
        public double PercentFee { get; set; }
    }
}
