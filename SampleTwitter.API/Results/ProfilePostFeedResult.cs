using SampleTwitter.API.DTOs.ResponseDTOs;

namespace SampleTwitter.API.Results;

public record ProfilePostFeedResult(IReadOnlyList<PostFeedItemDto> Items, PostAuthorDto Author);