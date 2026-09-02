namespace SampleTwitter.API.Models;

public class Post
{
    public long Id { get; set; }
    public string? Text { get; set; }
    public string? ImageUrl { get; set; }
    public long? ReplyId { get; set; }
    public Post? Reply { get; set; }
    public long UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}