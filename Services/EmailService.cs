using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace VorTech.App.Services
{
    public sealed class EmailService
    {
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
    }
}
