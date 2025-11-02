using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VorTech.App.Models;


namespace VorTech.App.Services
{
    public sealed class EmailService
    {
        // Gestion BDD pour les logs emails
        public EmailService()
        {
            EnsureSchema();
        }
        private void EnsureSchema()
        {
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS EmailLogs(
  Id           INTEGER PRIMARY KEY AUTOINCREMENT,
  SentAt       TEXT    NOT NULL,
  FromAddress  TEXT    NOT NULL,
  ToAddress    TEXT    NOT NULL,
  Subject      TEXT    NOT NULL,
  Attachments  TEXT    NOT NULL,
  Status       TEXT    NOT NULL,  -- 'OK' / 'ERROR'
  Error        TEXT    NULL,
  Context      TEXT    NOT NULL   -- ex: 'DEVIS:12345'
);";
            cmd.ExecuteNonQuery();
        }

        private readonly SettingsCatalogService _catalog = new SettingsCatalogService();

        // Remplacement {{Var}} / {{Obj.Prop}} à partir d'un dictionnaire "clé -> valeur"
        public string Render(string template, IDictionary<string, string> vars)
        {
            if (string.IsNullOrEmpty(template) || vars == null) return template ?? "";
            var s = template;
            foreach (var kv in vars)
                s = s.Replace("{{" + kv.Key + "}}", kv.Value ?? "", StringComparison.OrdinalIgnoreCase);
            return s;
        }

        public async Task SendAsync(
            string toEmail,
            string toName,
            string subject,
            string body,
            bool isHtml,
            int? accountId = null,
            IEnumerable<string>? attachments = null)
        {
            // 1) choisir le compte
            var accounts = _catalog.GetEmailAccounts();
            var acc = accountId.HasValue ? accounts.FirstOrDefault(a => a.Id == accountId.Value)
                                         : accounts.FirstOrDefault(a => a.IsDefault) ?? accounts.FirstOrDefault();
            if (acc == null)
                throw new InvalidOperationException("Aucun compte e-mail configuré.");

            // 2) construire le message
            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress(acc.DisplayName, acc.Address));
            msg.To.Add(new MailboxAddress(toName ?? "", toEmail));
            msg.Subject = subject ?? "";

            var builder = new BodyBuilder();
            if (isHtml) builder.HtmlBody = body ?? "";
            else builder.TextBody = body ?? "";

            if (attachments != null)
            {
                foreach (var path in attachments)
                {
                    if (!string.IsNullOrWhiteSpace(path) && System.IO.File.Exists(path))
                        builder.Attachments.Add(path);
                }
            }
            msg.Body = builder.ToMessageBody();

            // 3) envoi SMTP
            using var smtp = new SmtpClient();
            var secure = acc.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;
            await smtp.ConnectAsync(acc.SmtpHost, acc.SmtpPort, secure);
            if (!string.IsNullOrWhiteSpace(acc.Username))
                await smtp.AuthenticateAsync(acc.Username, acc.Password);
            await smtp.SendAsync(msg);
            await smtp.DisconnectAsync(true);
        }

        // Utilitaire: envoi par modèle
        public async Task SendFromTemplateAsync(
            int templateId,
            string toEmail,
            string toName,
            IDictionary<string,string> vars,
            int? accountId = null,
            IEnumerable<string>? attachments = null)
        {
            var t = _catalog.GetEmailTemplates().FirstOrDefault(x => x.Id == templateId);
            if (t == null) throw new InvalidOperationException("Modèle d’email introuvable.");

            var subject = Render(t.Subject, vars);
            var body    = Render(t.Body,    vars);

            await SendAsync(toEmail, toName, subject, body, t.IsHtml, accountId, attachments);
        }

        // Histoirsation des mails envoyer
        public async Task<int> SendAndLogAsync(
            EmailAccount account,
            string to,
            string subject,
            string body,
            bool isHtml,
            IEnumerable<string>? attachmentPaths,
            string context // ex: $"DEVIS:{devisId}"
        )
        {
            // 1) Compose
            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress(account.DisplayName ?? "", account.Address));
            msg.To.Add(MailboxAddress.Parse(to));
            msg.Subject = subject ?? "";

            var builder = new BodyBuilder();
            if (isHtml) builder.HtmlBody = body ?? "";
            else builder.TextBody = body ?? "";

            if (attachmentPaths != null)
            {
                foreach (var p in attachmentPaths)
                {
                    if (!string.IsNullOrWhiteSpace(p) && File.Exists(p))
                        builder.Attachments.Add(p);
                }
            }
            msg.Body = builder.ToMessageBody();

            var attachmentsList = attachmentPaths == null ? "" : string.Join(";", attachmentPaths.Where(File.Exists));

            // 2) Envoi
            string status = "OK";
            string? error = null;

            try
            {
                using var smtp = new SmtpClient();
                var sec = account.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;
                await smtp.ConnectAsync(account.SmtpHost, account.SmtpPort, sec);
                if (!string.IsNullOrEmpty(account.Username))
                    await smtp.AuthenticateAsync(account.Username, account.Password);
                await smtp.SendAsync(msg);
                await smtp.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                status = "ERROR";
                error = ex.Message;
            }

            // 3) Log
            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
INSERT INTO EmailLogs(SentAt, FromAddress, ToAddress, Subject, Attachments, Status, Error, Context)
VALUES(@t,@f,@to,@s,@a,@st,@e,@c);
SELECT last_insert_rowid();";
            Db.AddParam(cmd, "@t", DateTime.Now.ToString("s"));
            Db.AddParam(cmd, "@f", account.Address ?? "");
            Db.AddParam(cmd, "@to", to ?? "");
            Db.AddParam(cmd, "@s", subject ?? "");
            Db.AddParam(cmd, "@a", attachmentsList);
            Db.AddParam(cmd, "@st", status);
            Db.AddParam(cmd, "@e", (object?)error ?? DBNull.Value);
            Db.AddParam(cmd, "@c", context ?? "");
            var logId = Convert.ToInt32(cmd.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);

            if (status == "ERROR") throw new InvalidOperationException(error);
            return logId;
        }

        public string RenderTemplate(string src, IDictionary<string, string?> map)
        {
            if (string.IsNullOrEmpty(src)) return "";
            string res = src;
            foreach (var kv in map)
            {
                res = res.Replace("{{" + kv.Key + "}}", kv.Value ?? "", StringComparison.OrdinalIgnoreCase);
            }
            return res;
        }

        // Listes des email envoyer par clients
        public static List<EmailLog> GetLogsByToAddress(string toAddress)
        {
            var list = new List<EmailLog>();
            if (string.IsNullOrWhiteSpace(toAddress))
                return list;

            using var cn = Db.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
        SELECT *
        FROM EmailLogs
        WHERE ToAddress=@to
        ORDER BY SentAt DESC;";
            Db.AddParam(cmd, "@to", toAddress);
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
                list.Add(new EmailLog
                {
                    Id = Convert.ToInt32(rd["Id"]),
                    SentAt = DateTime.Parse(rd["SentAt"]?.ToString() ?? DateTime.Now.ToString("s")),
                    FromAddress = rd["FromAddress"]?.ToString() ?? "",
                    ToAddress = rd["ToAddress"]?.ToString() ?? "",
                    Subject = rd["Subject"]?.ToString() ?? "",
                    Attachments = rd["Attachments"]?.ToString() ?? "",
                    Status = rd["Status"]?.ToString() ?? "",
                    Error = rd["Error"] == DBNull.Value ? null : rd["Error"]?.ToString(),
                    Context = rd["Context"]?.ToString() ?? ""
                });
            return list;
        }

        public static List<EmailLog> GetLogsByContexts(List<string> contexts)
        {
            var list = new List<EmailLog>();
            if (contexts == null || contexts.Count == 0)
                return list;

            using var cn = Db.Open();
            foreach (var ctx in contexts)
            {
                using var cmd = cn.CreateCommand();
                cmd.CommandText = @"
            SELECT *
            FROM EmailLogs
            WHERE Context=@ctx
            ORDER BY SentAt DESC;";
                Db.AddParam(cmd, "@ctx", ctx);
                using var rd = cmd.ExecuteReader();
                while (rd.Read())
                    list.Add(new EmailLog
                    {
                        Id = Convert.ToInt32(rd["Id"]),
                        SentAt = DateTime.Parse(rd["SentAt"]?.ToString() ?? DateTime.Now.ToString("s")),
                        FromAddress = rd["FromAddress"]?.ToString() ?? "",
                        ToAddress = rd["ToAddress"]?.ToString() ?? "",
                        Subject = rd["Subject"]?.ToString() ?? "",
                        Attachments = rd["Attachments"]?.ToString() ?? "",
                        Status = rd["Status"]?.ToString() ?? "",
                        Error = rd["Error"] == DBNull.Value ? null : rd["Error"]?.ToString(),
                        Context = rd["Context"]?.ToString() ?? ""
                    });
            }
            return list;
        }
    }
}
