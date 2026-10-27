using SampleTwitter.API.Exceptions;

namespace SampleTwitter.API.Models;

public class Post
{
    public const int MaxTextLength = 280;

    private readonly List<Repost> _reposts = new();
    private readonly List<Post> _replies = new();
    private readonly List<Bookmark> _bookmarks = new();

    public long Id { get; private set; }
    public string? Text { get; private set; }
    public string? ImageUrl { get; private set; }
    public long? ReplyId { get; private set; }
    public Post? Reply { get; private set; }
    public long UserId { get; private set; }
    public User User { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public IReadOnlyCollection<Repost> Reposts => _reposts;
    public IReadOnlyCollection<Post> Replies => _replies;
    public IReadOnlyCollection<Bookmark> Bookmarks => _bookmarks;

    /// <summary>
    /// Used by EF Core for materialization. Application code must use <see cref="Create"/>.
    /// </summary>
    private Post()
    {
    }

    public static Post Create(
        long userId,
        string? text,
        string? imageUrl,
        long? replyId,
        DateTimeOffset? now = null)
    {
        var (normalizedText, normalizedImageUrl) = NormalizeContent(
            text,
            imageUrl,
            "User attempted to create a post with no text and no image.");

        return new Post
        {
            UserId = userId,
            Text = normalizedText,
            ImageUrl = normalizedImageUrl,
            ReplyId = replyId,
            CreatedAt = now ?? DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Replaces the post content. Only the author may edit the post,
    /// and the post must still contain text or an image afterwards.
    /// </summary>
    public void Edit(long editorUserId, string? text, string? imageUrl, DateTimeOffset? now = null)
    {
        EnsureOwnedBy(editorUserId, "edit");

        var (normalizedText, normalizedImageUrl) = NormalizeContent(
            text,
            imageUrl,
            "User attempted to edit a post to have no text and no image.");

        Text = normalizedText;
        ImageUrl = normalizedImageUrl;
        UpdatedAt = now ?? DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Soft-deletes the post. Only the author may delete the post.
    /// Deleting an already deleted post is a no-op.
    /// </summary>
    public void Delete(long actorUserId, DateTimeOffset? now = null)
    {
        EnsureOwnedBy(actorUserId, "delete");

        if (IsDeleted)
        {
            return;
        }

        IsDeleted = true;
        DeletedAt = now ?? DateTimeOffset.UtcNow;
    }

    private void EnsureOwnedBy(long actorUserId, string action)
    {
        if (UserId != actorUserId)
        {
            throw new ForbiddenException(
                $"User {actorUserId} attempted to {action} post {Id} owned by user {UserId}.");
        }
    }

    private static (string? Text, string? ImageUrl) NormalizeContent(
        string? text,
        string? imageUrl,
        string emptyPostMessage)
    {
        var normalizedText = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        var normalizedImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();

        if (normalizedText is null && normalizedImageUrl is null)
        {
            throw new EmptyPostException(emptyPostMessage);
        }

        if (normalizedText is not null && normalizedText.Length > MaxTextLength)
        {
            throw new ArgumentException($"Post text cannot exceed {MaxTextLength} characters.", nameof(text));
        }

        return (normalizedText, normalizedImageUrl);
    }
}
