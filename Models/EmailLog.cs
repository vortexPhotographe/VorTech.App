namespace VorTech.App.Models
{
    public class EmailLog
    {
        public int Id { get; set; }
        public DateTime SentAt { get; set; }
        public string FromAddress { get; set; } = "";
        public string ToAddress { get; set; } = "";
        public string Subject { get; set; } = "";
        public string Attachments { get; set; } = ""; // noms séparés par ';'
        public string Status { get; set; } = "OK";    // "OK" | "ERROR"
        public string? Error { get; set; }            // texte d’erreur éventuel
        public string Context { get; set; } = "";     // ex: "DEVIS:12345"
    }
}
