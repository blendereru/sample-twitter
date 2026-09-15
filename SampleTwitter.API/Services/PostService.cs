using Microsoft.EntityFrameworkCore;
using SampleTwitter.API.Abstractions;
using SampleTwitter.API.Data;
using SampleTwitter.API.DTOs.RequestDTOs;
using SampleTwitter.API.DTOs.ResponseDTOs;
using SampleTwitter.API.Exceptions;
using SampleTwitter.API.Models;
using SampleTwitter.API.Results;

namespace SampleTwitter.API.Services;

public class PostService : IPostService
{
    private readonly ApplicationContext _applicationContext;
    private readonly ILogger<PostService> _logger;

    public PostService(ApplicationContext applicationContext, ILogger<PostService> logger)
    {
        _applicationContext = applicationContext;
        _logger = logger;
    }

    public async Task<CreatePostResult> Create(CreatePostRequest request, long userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Text) && string.IsNullOrWhiteSpace(request.ImageUrl))
        {
            throw new EmptyPostException("User attempted to create a post with no text and no image.");
        }

        if (request.ReplyId is not null)
        {
            var parentExists = await _applicationContext.Posts
                .AnyAsync(p => p.Id == request.ReplyId, ct);

            if (!parentExists)
            {
                _logger.LogWarning("Reply target post {ReplyId} not found", request.ReplyId);
                throw new PostNotFoundException($"Post with id {request.ReplyId} was not found.");
            }
        }

        var post = new Post
        {
            Text = request.Text?.Trim(),
            ImageUrl = request.ImageUrl,
            ReplyId = request.ReplyId,
            CreatedAt = DateTimeOffset.UtcNow,
            UserId = userId
        };

        _applicationContext.Posts.Add(post);
        await _applicationContext.SaveChangesAsync(ct);

        _logger.LogInformation("Post {PostId} created by user {UserId}", post.Id, userId);

        return new CreatePostResult(
            post.Id,
            post.Text,
            post.ImageUrl,
            post.ReplyId,
            post.UserId,
            post.CreatedAt);
    }

    public async Task<EditPostResult> Edit(long postId, EditPostRequest request, long userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Text) && string.IsNullOrWhiteSpace(request.ImageUrl))
        {
            throw new EmptyPostException("User attempted to edit a post to have no text and no image.");
        }

        var post = await _applicationContext.Posts
            .SingleOrDefaultAsync(p => p.Id == postId, ct);

        if (post is null)
        {
            _logger.LogWarning("Edit failed — post {PostId} not found", postId);
            throw new PostNotFoundException($"Post with id {postId} was not found.");
        }

        if (post.UserId != userId)
        {
            _logger.LogWarning("Edit forbidden — user {UserId} does not own post {PostId}", userId, postId);
            throw new ForbiddenException($"User {userId} attempted to edit post {postId} owned by user {post.UserId}.");
        }

        post.Text = request.Text?.Trim();
        post.ImageUrl = request.ImageUrl;
        post.UpdatedAt = DateTimeOffset.UtcNow;

        await _applicationContext.SaveChangesAsync(ct);

        _logger.LogInformation("Post {PostId} edited by user {UserId}", post.Id, userId);

        return new EditPostResult(
            post.Id,
            post.Text,
            post.ImageUrl,
            post.UpdatedAt);
    }

    public async Task<PostFeedResult> GetProfileFeed(
        long userId,
        CancellationToken ct = default)
    {
        if (!await _applicationContext.Users.AnyAsync(u => u.Id == userId, ct))
        {
            _logger.LogWarning("Profile feed requested for non-existent user {UserId}", userId);
            throw new UserNotFoundException($"User with id {userId} was not found.");
        }

        var authoredPosts = await _applicationContext.Posts
            .AsNoTracking()
            .Include(p => p.User)
            .Include(p => p.Reply!)
                .ThenInclude(r => r.User)
            .Where(p => p.UserId == userId
                        && (p.ReplyId == null || p.Reply!.UserId == userId))
            .ToListAsync(ct);

        var reposts = await _applicationContext.Reposts
            .AsNoTracking()
            .Include(r => r.User)
            .Include(r => r.Post)
                .ThenInclude(p => p.User)
            .Include(r => r.Post)
                .ThenInclude(p => p.Reply!)
                    .ThenInclude(r => r.User)
            .Where(r => r.UserId == userId)
            .ToListAsync(ct);

        var allPostIds = authoredPosts
            .Select(p => p.Id)
            .Concat(authoredPosts.Where(p => p.ReplyId.HasValue).Select(p => p.ReplyId!.Value))
            .Concat(reposts.Select(r => r.PostId))
            .Concat(reposts.Where(r => r.Post.ReplyId.HasValue).Select(r => r.Post.ReplyId!.Value))
            .Distinct()
            .ToList();

        var repostCounts = allPostIds.Count > 0
            ? await _applicationContext.Reposts
                .Where(r => allPostIds.Contains(r.PostId))
                .GroupBy(r => r.PostId)
                .Select(g => new { PostId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.PostId, x => x.Count, ct)
            : new Dictionary<long, int>();

        var replyCounts = allPostIds.Count > 0
            ? await _applicationContext.Posts
                .Where(p => p.ReplyId.HasValue && allPostIds.Contains(p.ReplyId.Value))
                .GroupBy(p => p.ReplyId!.Value)
                .Select(g => new { PostId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.PostId, x => x.Count, ct)
            : new Dictionary<long, int>();

        var authoredItems = authoredPosts.Select(p => new
        {
            DisplayTimestamp = p.CreatedAt,
            p.Id,
            Dto = MapToFeedItem(p, repostCounts, replyCounts)
        });

        var repostItems = reposts.Select(r => new
        {
            DisplayTimestamp = r.CreatedAt,
            Id = r.PostId,
            Dto = MapToRepostFeedItem(r, repostCounts, replyCounts)
        });

        var feed = authoredItems
            .Concat(repostItems)
            .OrderByDescending(x => x.DisplayTimestamp)
            .ThenByDescending(x => x.Id)
            .Select(x => x.Dto)
            .ToList();

        return new PostFeedResult(feed);
    }

    public async Task Delete(long postId, long userId, CancellationToken ct = default)
    {
        var post = await _applicationContext.Posts
            .SingleOrDefaultAsync(p => p.Id == postId, ct);

        if (post is null)
        {
            _logger.LogWarning("Delete failed — post {PostId} not found", postId);
            throw new PostNotFoundException($"Post with id {postId} was not found.");
        }

        if (post.UserId != userId)
        {
            _logger.LogWarning("Delete forbidden — user {UserId} does not own post {PostId}", userId, postId);
            throw new ForbiddenException($"User {userId} attempted to delete post {postId} owned by user {post.UserId}.");
        }

        post.IsDeleted = true;
        post.DeletedAt = DateTimeOffset.UtcNow;
        await _applicationContext.SaveChangesAsync(ct);

        _logger.LogInformation("Post {PostId} deleted by user {UserId}", post.Id, userId);
    }

    public async Task<RepostResult> Repost(long postId, long userId, CancellationToken ct = default)
    {
        var post = await _applicationContext.Posts
            .SingleOrDefaultAsync(p => p.Id == postId, ct);

        if (post is null)
        {
            _logger.LogWarning("Repost failed — post {PostId} not found", postId);
            throw new PostNotFoundException($"Post with id {postId} was not found.");
        }

        var userExists = await _applicationContext.Users
            .AnyAsync(u => u.Id == userId, ct);

        if (!userExists)
        {
            _logger.LogWarning("Repost failed — user {UserId} not found", userId);
            throw new UserNotFoundException($"User with id {userId} was not found.");
        }

        var alreadyReposted = await _applicationContext.Reposts
            .AnyAsync(r => r.PostId == postId && r.UserId == userId, ct);

        if (alreadyReposted)
        {
            _logger.LogWarning("Repost failed — user {UserId} has already reposted post {PostId}", userId, postId);
            throw new AlreadyRepostedException($"User {userId} has already reposted post {postId}.");
        }

        var repost = new Repost
        {
            PostId = postId,
            UserId = userId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _applicationContext.Reposts.Add(repost);
        await _applicationContext.SaveChangesAsync(ct);

        _logger.LogInformation("Post {PostId} reposted by user {UserId}", postId, userId);

        return new RepostResult(repost.PostId, repost.UserId, repost.CreatedAt);
    }

    public async Task UndoRepost(long postId, long userId, CancellationToken ct = default)
    {
        var repost = await _applicationContext.Reposts
            .SingleOrDefaultAsync(r => r.PostId == postId && r.UserId == userId, ct);

        if(repost is null)
        {
            _logger.LogWarning(
                "Undo repost failed - post {PostId} not found or user {UserId} hasn't reposted this post.", postId, userId);
            throw new RepostNotFoundException($"Repost for post {postId} by user {userId} was not found.");
        }

        _applicationContext.Reposts.Remove(repost);
        await _applicationContext.SaveChangesAsync(ct);

        _logger.LogInformation("Repost for post {PostId} by user {UserId} undone", postId, userId);
    }

    public async Task<PostFeedResult> GetReplies(long postId, CancellationToken ct = default)
    {
        if (!await _applicationContext.Posts.AnyAsync(p => p.Id == postId, ct))
        {
            _logger.LogWarning("Replies requested for non-existent post {PostId}", postId);
            throw new PostNotFoundException($"Post with id {postId} was not found.");
        }

        var replies = await _applicationContext.Posts
            .AsNoTracking()
            .Include(p => p.User)
            .Where(p => p.ReplyId == postId)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(ct);

        var replyIds = replies.Select(p => p.Id).ToList();

        var repostCounts = replyIds.Count > 0
            ? await _applicationContext.Reposts
                .Where(r => replyIds.Contains(r.PostId))
                .GroupBy(r => r.PostId)
                .Select(g => new { PostId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.PostId, x => x.Count, ct)
            : new Dictionary<long, int>();

        var replyCounts = replyIds.Count > 0
            ? await _applicationContext.Posts
                .Where(p => p.ReplyId.HasValue && replyIds.Contains(p.ReplyId.Value))
                .GroupBy(p => p.ReplyId!.Value)
                .Select(g => new { PostId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.PostId, x => x.Count, ct)
            : new Dictionary<long, int>();

        var items = replies
            .Select(r => MapToFeedItem(r, repostCounts, replyCounts))
            .ToList();

        return new PostFeedResult(items);
    }

    private static PostFeedItemDto MapToFeedItem(Post post, IReadOnlyDictionary<long, int> repostCounts,
        IReadOnlyDictionary<long, int> replyCounts) =>
        new(
            post.Id,
            post.Text,
            post.ImageUrl,
            post.CreatedAt,
            post.UpdatedAt,
            new PostAuthorDto(post.User.Id, post.User.Email),
            post.Reply is not null ? MapToFeedItem(post.Reply, repostCounts, replyCounts) : null,
            false,
            null,
            repostCounts.GetValueOrDefault(post.Id, 0),
            replyCounts.GetValueOrDefault(post.Id, 0)
        );

    private static PostFeedItemDto MapToRepostFeedItem(
        Repost repost,
        IReadOnlyDictionary<long, int> repostCounts,
        IReadOnlyDictionary<long, int> replyCounts) =>
        new(
            repost.Post.Id,
            repost.Post.Text,
            repost.Post.ImageUrl,
            repost.Post.CreatedAt,
            repost.Post.UpdatedAt,
            new PostAuthorDto(repost.Post.User.Id, repost.Post.User.Email),
            repost.Post.Reply is not null ? MapToFeedItem(repost.Post.Reply, repostCounts, replyCounts) : null,
            true,
            new PostAuthorDto(repost.User.Id, repost.User.Email),
            repostCounts.GetValueOrDefault(repost.Post.Id, 0),
            replyCounts.GetValueOrDefault(repost.Post.Id, 0)
        );
}