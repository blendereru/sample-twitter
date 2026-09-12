using SampleTwitter.API.DTOs.ResponseDTOs;

namespace SampleTwitter.API.Results;

public record PostFeedResult(IReadOnlyList<PostFeedItemDto> Items);