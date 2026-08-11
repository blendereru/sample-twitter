namespace SampleTwitter.API.Options;

public class EmailOptions
{
    public required string SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public required string SmtpUsername { get; set; }
    public required string SmtpPassword { get; set; }
    public required string FromAddress { get; set; }
    public required string FromName { get; set; }
}