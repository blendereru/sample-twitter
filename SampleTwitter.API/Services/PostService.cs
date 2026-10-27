using Microsoft.EntityFrameworkCore;
using Npgsql;
using SampleTwitter.API.Abstractions;
using SampleTwitter.API.Data;
using SampleTwitter.API.DTOs.RequestDTOs;
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
        var post = Post.Create(userId, request.Text, request.ImageUrl, request.ReplyId);

        if (post.ReplyId is not null)
        {
            var parentExists = await _applicationContext.Posts
                .AnyAsync(p => p.Id == post.ReplyId, ct);

            if (!parentExists)
            {
                _logger.LogWarning("Reply target post {ReplyId} not found", post.ReplyId);
                throw new PostNotFoundException($"Post with id {post.ReplyId} was not found.");
            }
        }

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
        var post = await _applicationContext.Posts
            .SingleOrDefaultAsync(p => p.Id == postId, ct);

        if (post is null)
        {
            _logger.LogWarning("Edit failed — post {PostId} not found", postId);
            throw new PostNotFoundException($"Post with id {postId} was not found.");
        }

        post.Edit(userId, request.Text, request.ImageUrl);

        await _applicationContext.SaveChangesAsync(ct);

        _logger.LogInformation("Post {PostId} edited by user {UserId}", post.Id, userId);

        return new EditPostResult(
            post.Id,
            post.Text,
            post.ImageUrl,
            post.UpdatedAt);
    }

    public async Task<ProfilePostFeedResult> GetPosts(long userId, long? viewerUserId = null, CancellationToken ct = default)
    {
        var user = await _applicationContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null)
        {
            _logger.LogWarning("Profile feed requested for non-existent user {UserId}", userId);
            throw new UserNotFoundException($"User with id {userId} was not found.");
        }

        var posts = await _applicationContext.Posts
            .Where(p => p.UserId == user.Id && p.ReplyId == null)
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.Id)
            .Select(p => new PostFeedItemResult(
                    p.Id,
                    p.Text,
                    p.ImageUrl,
                    p.CreatedAt,
                    p.UpdatedAt,
                    p.Reposts.Count,
                    p.Replies.Count,
                    viewerUserId.HasValue && p.Bookmarks.Any(b => b.UserId == viewerUserId.Value)))
            .ToListAsync(ct);

        return new ProfilePostFeedResult(posts, new PostAuthorResult(user.Id, user.Email));
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

        post.Delete(userId);

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

        var repost = Models.Repost.Create(postId, userId);

        _applicationContext.Reposts.Add(repost);

        try
        {
            await _applicationContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            _logger.LogWarning(ex, "Race condition: duplicate repost attempt detected during save for post {PostId} by user {UserId}", postId, userId);
            throw new AlreadyRepostedException($"User {userId} has already reposted post {postId} (race condition).");
        }

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

    public async Task<PostResult> GetById(long id, long? viewerUserId = null, CancellationToken ct = default)
    {
        var post = await _applicationContext.Posts
            .Where(p => p.Id == id)
            .Select(p => new PostResult(
                p.Id,
                p.Text,
                p.ImageUrl,
                p.CreatedAt,
                p.UpdatedAt,
                new PostAuthorResult(p.User.Id, p.User.Email),
                p.ReplyId,
                p.Reposts.Count,
                p.Replies.Count,
                viewerUserId.HasValue && p.Bookmarks.Any(b => b.UserId == viewerUserId.Value)))
            .SingleOrDefaultAsync(ct);

        if (post is null)
        {
            _logger.LogWarning("Post {PostId} not found", id);
            throw new PostNotFoundException($"Post with id {id} was not found.");
        }

        return post;
    }

    public async Task<ProfileReplyFeedResult> GetProfileReplies(long userId, long? viewerUserId = null, CancellationToken ct = default)
    {
        var user = await _applicationContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == userId, ct);

        if(user is null)
        {
            _logger.LogWarning("Replies feed requested for non-existent user {UserId}", userId);
            throw new UserNotFoundException($"User with id {userId} was not found.");
        }

        var repliesFeed = await _applicationContext.Posts
            .Where(p => p.UserId == userId && p.ReplyId != null)
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.Id)
            .Select(p => new ReplyPostResult(
                p.Id,
                p.Text,
                p.ImageUrl,
                p.CreatedAt,
                p.UpdatedAt,
                p.Reply == null
                    ? null
                    : new PostResult(
                        p.Reply.Id,
                        p.Reply.Text,
                        p.Reply.ImageUrl,
                        p.Reply.CreatedAt,
                        p.Reply.UpdatedAt,
                        new PostAuthorResult(p.Reply.UserId, p.Reply.User.Email),
                        ReplyId: null,
                        p.Reply.Reposts.Count,
                        p.Reply.Replies.Count,
                        viewerUserId.HasValue && p.Reply.Bookmarks.Any(b => b.UserId == viewerUserId.Value)),
                p.Reposts.Count,
                p.Replies.Count,
                viewerUserId.HasValue && p.Bookmarks.Any(b => b.UserId == viewerUserId.Value)))
            .ToListAsync(ct);

        return new ProfileReplyFeedResult(repliesFeed, new PostAuthorResult(user.Id, user.Email));
    }

    public async Task<List<ReplyFeedResult>> GetReplies(long postId, long? viewerUserId = null, CancellationToken ct = default)
    {
        var postExists = await _applicationContext.Posts
            .AnyAsync(p => p.Id == postId, ct);
        if (!postExists)
        {
            _logger.LogWarning("Replies requested for non-existent post {PostId}", postId);
            throw new PostNotFoundException($"Post with id {postId} was not found.");
        }

        return await _applicationContext.Posts
            .Where(p => p.ReplyId == postId)
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.Id)
            .Select(p => new ReplyFeedResult(
                p.Id,
                p.Text,
                p.ImageUrl,
                p.CreatedAt,
                p.UpdatedAt,
                new PostAuthorResult(p.UserId, p.User.Email),
                p.Reposts.Count,
                p.Replies.Count,
                viewerUserId.HasValue && p.Bookmarks.Any(b => b.UserId == viewerUserId.Value)))
            .ToListAsync(ct);
    }

    public async Task<BookmarkResult> Bookmark(long postId, long userId, CancellationToken ct = default)
    {
        var post = await _applicationContext.Posts
            .SingleOrDefaultAsync(p => p.Id == postId, ct);

        if (post is null)
        {
            _logger.LogWarning("Bookmark failed — post {PostId} not found", postId);
            throw new PostNotFoundException($"Post with id {postId} was not found.");
        }

        var userExists = await _applicationContext.Users
            .AnyAsync(u => u.Id == userId, ct);

        if (!userExists)
        {
            _logger.LogWarning("Bookmark failed — user {UserId} not found", userId);
            throw new UserNotFoundException($"User with id {userId} was not found.");
        }

        var alreadyBookmarked = await _applicationContext.Bookmarks
            .AnyAsync(b => b.PostId == postId && b.UserId == userId, ct);

        if (alreadyBookmarked)
        {
            _logger.LogWarning("Bookmark failed — user {UserId} has already bookmarked post {PostId}", userId, postId);
            throw new AlreadyBookmarkedException($"User {userId} has already bookmarked post {postId}.");
        }

        var bookmark = Models.Bookmark.Create(postId, userId);

        _applicationContext.Bookmarks.Add(bookmark);

        try
        {
            await _applicationContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            _logger.LogWarning(ex, "Race condition: duplicate bookmark attempt detected during save for post {PostId} by user {UserId}", postId, userId);
            throw new AlreadyBookmarkedException($"User {userId} has already bookmarked post {postId} (race condition).");
        }

        _logger.LogInformation("Post {PostId} bookmarked by user {UserId}", postId, userId);

        return new BookmarkResult(bookmark.PostId, bookmark.UserId, bookmark.CreatedAt);
    }

    public async Task UndoBookmark(long postId, long userId, CancellationToken ct = default)
    {
        var bookmark = await _applicationContext.Bookmarks
            .SingleOrDefaultAsync(b => b.PostId == postId && b.UserId == userId, ct);

        if (bookmark is null)
        {
            _logger.LogWarning(
                "Undo bookmark failed - post {PostId} not found or user {UserId} hasn't bookmarked this post.", postId, userId);
            throw new BookmarkNotFoundException($"Bookmark for post {postId} by user {userId} was not found.");
        }

        _applicationContext.Bookmarks.Remove(bookmark);
        await _applicationContext.SaveChangesAsync(ct);

        _logger.LogInformation("Bookmark for post {PostId} by user {UserId} undone", postId, userId);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        return ex.InnerException is PostgresException pgEx && pgEx.SqlState == PostgresErrorCodes.UniqueViolation;
    }
}