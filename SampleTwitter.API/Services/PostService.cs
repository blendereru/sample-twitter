using Microsoft.EntityFrameworkCore;
using SampleTwitter.API.Abstractions;
using SampleTwitter.API.Data;
using SampleTwitter.API.DTOs.RequestDTOs;
using SampleTwitter.API.Exceptions;
using SampleTwitter.API.Models;

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
    
    public async Task<Post> Create(CreatePostRequest request, long userId, CancellationToken ct = default)
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

        return post;
    }

    public async Task<Post> Edit(long postId, EditPostRequest request, long userId, CancellationToken ct = default)
    {
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

        if (string.IsNullOrWhiteSpace(request.Text) && string.IsNullOrWhiteSpace(request.ImageUrl))
        {
            throw new EmptyPostException("User attempted to edit a post to have no text and no image.");
        }

        post.Text = request.Text?.Trim();
        post.ImageUrl = request.ImageUrl;
        post.UpdatedAt = DateTimeOffset.UtcNow;

        await _applicationContext.SaveChangesAsync(ct);

        _logger.LogInformation("Post {PostId} edited by user {UserId}", post.Id, userId);

        return post;
    }
}