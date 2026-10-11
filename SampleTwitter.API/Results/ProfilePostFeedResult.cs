namespace SampleTwitter.API.Results;

public record ProfilePostFeedResult(IReadOnlyList<PostFeedItemResult> Items, PostAuthorResult Author);