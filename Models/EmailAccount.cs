public sealed class EmailAccount
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = "";
    public string Address { get; set; } = "";
    public string SmtpHost { get; set; } = "";
    public int SmtpPort { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public bool IsDefault { get; set; }
}