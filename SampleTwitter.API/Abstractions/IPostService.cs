using SampleTwitter.API.DTOs.RequestDTOs;
using SampleTwitter.API.Results;

namespace SampleTwitter.API.Abstractions;

public interface IPostService
{
    Task<CreatePostResult> Create(CreatePostRequest request, long userId, CancellationToken ct = default);
    Task<EditPostResult> Edit(long postId, EditPostRequest request, long userId, CancellationToken ct = default);
    Task<PostFeedResult> GetProfileFeed(long userId, CancellationToken ct = default);
    Task Delete(long postId, long userId, CancellationToken ct = default);
    Task<RepostResult> Repost(long postId, long userId, CancellationToken ct = default);
}