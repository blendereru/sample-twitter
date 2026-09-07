namespace SampleTwitter.API.DTOs.ResponseDTOs;

/// <summary>
/// Feed of posts for a user profile timeline.
/// </summary>
public record PostFeedResponse(IReadOnlyList<PostFeedItemDto> Items);