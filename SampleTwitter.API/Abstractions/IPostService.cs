using SampleTwitter.API.DTOs.RequestDTOs;
using SampleTwitter.API.Results;

namespace SampleTwitter.API.Abstractions;

public interface IPostService
{
    Task<CreatePostResult> Create(CreatePostRequest request, long userId, CancellationToken ct = default);
    Task<EditPostResult> Edit(long postId, EditPostRequest request, long userId, CancellationToken ct = default);
    Task<ProfilePostFeedResult> GetPosts(long userId, long? viewerUserId = null, CancellationToken ct = default);
    Task Delete(long postId, long userId, CancellationToken ct = default);
    Task<RepostResult> Repost(long postId, long userId, CancellationToken ct = default);
    Task UndoRepost(long postId, long userId, CancellationToken ct = default);
    Task<PostResult> GetById(long id, long? viewerUserId = null, CancellationToken ct = default);
    Task<ProfileReplyFeedResult> GetProfileReplies(long userId, long? viewerUserId = null, CancellationToken ct = default);
    Task<List<ReplyFeedResult>> GetReplies(long postId, long? viewerUserId = null, CancellationToken ct = default);
    Task<BookmarkResult> Bookmark(long postId, long userId, CancellationToken ct = default);
    Task UndoBookmark(long postId, long userId, CancellationToken ct = default);
}
