using Microsoft.Data.Sqlite;
using System;
using System.Globalization;
using VorTech.App;
using Microsoft.Data.Sqlite;

namespace VorTech.App.Services
{
    public interface INumberingService
    {
        string Next(string docType, DateOnly docDate);
        (string pattern, string scope) GetFormat(string docType);
        void SetFormat(string docType, string pattern, string scope); // 👈 c’est ce nom-là
        int GetNextSeq(string docType, DateOnly date);
        void SetNextSeq(string docType, DateOnly date, int nextSeq);
    }

    public class NumberingService : INumberingService
    {
        public string Next(string docType, DateOnly docDate)
        {
            Logger.Info($"NUM.Next begin: type={docType}, date={docDate}");
            var (pattern, scope) = GetFormat(docType);
            var periodKey = GetPeriodKey(scope, docDate);
            Logger.Info($"NUM.Next: pattern={pattern}, scope={scope}, periodKey={periodKey}");

            using var cn = Db.Open();
            using var tx = cn.BeginTransaction();

            // upsert (si absent -> 1)
            using (var up = cn.CreateCommand())
            {
                up.Transaction = tx;
                up.CommandText = @"
INSERT INTO DocNumberingCounters(DocType,PeriodKey,NextSeq)
VALUES(@d,@p,1)
ON CONFLICT(DocType,PeriodKey) DO NOTHING;";
                Db.AddParam(up, "@d", docType);
                Db.AddParam(up, "@p", periodKey);
                up.ExecuteNonQuery();
            }

            int next;
            using (var sel = cn.CreateCommand())
            {
                sel.Transaction = tx;
                sel.CommandText = "SELECT NextSeq FROM DocNumberingCounters WHERE DocType=@d AND PeriodKey=@p;";
                Db.AddParam(sel, "@d", docType);
                Db.AddParam(sel, "@p", periodKey);
                next = Convert.ToInt32(sel.ExecuteScalar() ?? 1, CultureInfo.InvariantCulture);
            }
            Logger.Info($"NUM.Next: current seq={next}");
           
            // formater le numéro
            var numero = Format(pattern, docDate, next);

            // incrémenter
            using (var upd = cn.CreateCommand())
            {
                upd.Transaction = tx;
                upd.CommandText = "UPDATE DocNumberingCounters SET NextSeq=@n WHERE DocType=@d AND PeriodKey=@p;";
                Logger.Info($"NUM.Next: incremented to {next + 1}");
                Db.AddParam(upd, "@n", next + 1);
                Db.AddParam(upd, "@d", docType);
                Db.AddParam(upd, "@p", periodKey);
                upd.ExecuteNonQuery();
            }
            Logger.Info($"NUM.Next end -> {numero}");
            tx.Commit();
            return numero;
        }

        public (string pattern, string scope) GetFormat(string docType)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT Pattern, Scope FROM DocNumberingFormats WHERE DocType=@d;";
            Db.AddParam(cmd, "@d", docType);
            using var rd = cmd.ExecuteReader();
            if (rd.Read()) return (rd.GetString(0), rd.GetString(1));
            // défauts si non seedé
            return (docType + "-{yyyy}-{MM}-{####}", "MONTHLY");
        }

        public void SetFormat(string docType, string pattern, string scope)
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
INSERT INTO DocNumberingFormats(DocType,Pattern,Scope)
VALUES(@d,@p,@s)
ON CONFLICT(DocType) DO UPDATE SET Pattern=@p, Scope=@s;";
            Db.AddParam(cmd, "@d", docType);
            Db.AddParam(cmd, "@p", pattern);
            Db.AddParam(cmd, "@s", scope);
            cmd.ExecuteNonQuery();
        }

        public int GetNextSeq(string docType, DateOnly date)
        {
            var (_, scope) = GetFormat(docType);
            var key = GetPeriodKey(scope, date);
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT NextSeq FROM DocNumberingCounters WHERE DocType=@d AND PeriodKey=@p;";
            Db.AddParam(cmd, "@d", docType);
            Db.AddParam(cmd, "@p", key);
            var obj = cmd.ExecuteScalar();
            return obj == null ? 1 : Convert.ToInt32(obj, CultureInfo.InvariantCulture);
        }

        public void SaveFormat(string docType, string pattern, string scope)
        {
            using var cn = Db.Open();
            cn.Open();
            using var tx = cn.BeginTransaction();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
INSERT INTO DocNumberingFormats (DocType, Pattern, Scope)
VALUES (@t, @p, @s)
ON CONFLICT(DocType) DO UPDATE
SET Pattern = excluded.Pattern, Scope = excluded.Scope;";
            cmd.Parameters.AddWithValue("@t", docType);
            cmd.Parameters.AddWithValue("@p", pattern ?? "");
            cmd.Parameters.AddWithValue("@s", scope ?? "MONTHLY");
            cmd.ExecuteNonQuery();
            tx.Commit();
        }

        public void SetNextSeq(string docType, DateOnly date, int nextSeq)
        {
            if (nextSeq < 1) nextSeq = 1;
            var (_, scope) = GetFormat(docType);
            var key = GetPeriodKey(scope, date);
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
INSERT INTO DocNumberingCounters(DocType,PeriodKey,NextSeq)
VALUES(@d,@p,@n)
ON CONFLICT(DocType,PeriodKey) DO UPDATE SET NextSeq=@n;";
            Db.AddParam(cmd, "@d", docType);
            Db.AddParam(cmd, "@p", key);
            Db.AddParam(cmd, "@n", nextSeq);
            cmd.ExecuteNonQuery();
        }

        private static string GetPeriodKey(string scope, DateOnly date)
        {
            return scope?.ToUpperInvariant() switch
            {
                "GLOBAL" => "",
                "YEARLY" => date.ToString("yyyy", CultureInfo.InvariantCulture),
                _ => date.ToString("yyyy-MM", CultureInfo.InvariantCulture) // MONTHLY
            };
        }

        private static string Format(string pattern, DateOnly date, int seq)
        {
            // remplacements simples
            var s = pattern
                .Replace("{yyyy}", date.ToString("yyyy", CultureInfo.InvariantCulture))
                .Replace("{MM}", date.ToString("MM", CultureInfo.InvariantCulture))
                .Replace("{dd}", date.ToString("dd", CultureInfo.InvariantCulture));

            // {####...} = padding
            // on cherche la première occurrence
            var start = s.IndexOf('{');
            var end = s.IndexOf('}', start + 1);
            if (start >= 0 && end > start)
            {
                var inside = s.Substring(start + 1, end - start - 1);
                if (inside.Length >= 3 && inside.Trim('#').Length == 0)
                {
                    var width = inside.Length;
                    var pad = seq.ToString(new string('0', width), CultureInfo.InvariantCulture);
                    s = s.Substring(0, start) + pad + s.Substring(end + 1);
                }
            }
            return s;
        }
    }
}
