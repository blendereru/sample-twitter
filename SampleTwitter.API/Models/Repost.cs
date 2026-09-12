namespace SampleTwitter.API.Models;

public class Repost
{
    public long PostId { get; set; }
    public Post Post { get; set; } = null!;
    public long UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
}