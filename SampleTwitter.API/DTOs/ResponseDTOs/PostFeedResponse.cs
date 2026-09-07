namespace SampleTwitter.API.DTOs.ResponseDTOs;

public record PostFeedResponse(IReadOnlyList<PostFeedItemDto> Items);