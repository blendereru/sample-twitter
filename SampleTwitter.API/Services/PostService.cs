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
}