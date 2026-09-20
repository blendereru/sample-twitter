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

    public async Task<ProfilePostFeedResult> GetPosts(long userId, CancellationToken ct = default)
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
            .Select(p => new PostFeedItemDto(
                    p.Id,
                    p.Text,
                    p.ImageUrl,
                    p.CreatedAt,
                    p.UpdatedAt,
                    p.Reposts.Count,
                    p.Replies.Count))
            .ToListAsync(ct);

        return new ProfilePostFeedResult(posts, new PostAuthorDto(user.Id, user.Email));
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

    public async Task<PostResult> GetById(long id, CancellationToken ct = default)
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
                p.Replies.Count))
            .SingleOrDefaultAsync(ct);

        if (post is null)
        {
            _logger.LogWarning("Post {PostId} not found", id);
            throw new PostNotFoundException($"Post with id {id} was not found.");
        }

        return post;
    }
}