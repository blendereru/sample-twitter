namespace SampleTwitter.API.Models;

public class User
{
    public long Id { get; set; }
    public string Email { get; set; }
    public string PasswordHash { get; set; }
    public bool EmailConfirmed { get; set; } = false;
    public DateTimeOffset RegisteredAt { get; set; }
}