namespace SampleTwitter.API.DTOs.ResponseDTOs;

/// <summary>
/// Feed of replies for a user profile timeline.
/// </summary>
public record ReplyFeedResponse(IReadOnlyList<ReplyFeedItemDto> Items, PostAuthorDto Author);