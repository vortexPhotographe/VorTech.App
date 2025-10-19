using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using VorTech.App.Models;

namespace VorTech.App.Services
{
    public class BankAccountService
    {
        public List<BankAccount> GetAll()
        {
            var list = new List<BankAccount>();
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
SELECT Id, DisplayName, Iban, Bic, Holder, BankName, IsDefault
FROM BankAccounts
ORDER BY IsDefault DESC, DisplayName ASC;";
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                list.Add(Map(rd));
            }
            return list;
        }

        public int Insert(BankAccount b)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
INSERT INTO BankAccounts(DisplayName, Iban, Bic, Holder, BankName, IsDefault)
VALUES(@n, @i, @b, @h, @bn, @d);
SELECT last_insert_rowid();";
            Db.AddParam(cmd, "@n",  b.DisplayName ?? "");
            Db.AddParam(cmd, "@i",  b.Iban ?? "");
            Db.AddParam(cmd, "@b",  b.Bic ?? "");
            Db.AddParam(cmd, "@h",  b.Holder ?? "");
            Db.AddParam(cmd, "@bn", b.BankName ?? "");
            Db.AddParam(cmd, "@d",  b.IsDefault ? 1 : 0);
            return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        }

        public void Update(BankAccount b)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
UPDATE BankAccounts SET
  DisplayName=@n, Iban=@i, Bic=@b, Holder=@h, BankName=@bn, IsDefault=@d
WHERE Id=@id;";
            Db.AddParam(cmd, "@id", b.Id);
            Db.AddParam(cmd, "@n",  b.DisplayName ?? "");
            Db.AddParam(cmd, "@i",  b.Iban ?? "");
            Db.AddParam(cmd, "@b",  b.Bic ?? "");
            Db.AddParam(cmd, "@h",  b.Holder ?? "");
            Db.AddParam(cmd, "@bn", b.BankName ?? "");
            Db.AddParam(cmd, "@d",  b.IsDefault ? 1 : 0);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "DELETE FROM BankAccounts WHERE Id=@id;";
            Db.AddParam(cmd, "@id", id);
            cmd.ExecuteNonQuery();
        }

        private static BankAccount Map(IDataRecord r) => new BankAccount
        {
            Id          = Convert.ToInt32(r["Id"]),
            DisplayName = r["DisplayName"]?.ToString(),
            Iban        = r["Iban"]?.ToString(),
            Bic         = r["Bic"]?.ToString(),
            Holder      = r["Holder"]?.ToString(),
            BankName    = r["BankName"]?.ToString(),
            IsDefault   = Convert.ToInt32(r["IsDefault"]) == 1
        };
    }
}
