// Models/SmtpSettings.cs
public class SmtpSettings
{
    public required string SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public required string Username { get; set; }
    public required string Password { get; set; }
    public required string FromAddress { get; set; }
}