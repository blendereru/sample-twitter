using SampleTwitter.API.DTOs.RequestDTOs;
using SampleTwitter.API.DTOs.ResponseDTOs;
using SampleTwitter.API.Models;

namespace SampleTwitter.API.Abstractions;

public interface IPostService
{
    Task<Post> Create(CreatePostRequest request, long userId, CancellationToken ct = default);
    Task<Post> Edit(long postId, EditPostRequest request, long userId, CancellationToken ct = default);
    Task<PostFeedResponse> GetProfileFeed(long userId, CancellationToken ct = default);
    Task Delete(long postId, long userId, CancellationToken ct = default);
}