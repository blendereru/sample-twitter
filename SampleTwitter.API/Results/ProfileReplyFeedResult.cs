namespace SampleTwitter.API.Results;

public record ProfileReplyFeedResult(IReadOnlyList<ReplyPostResult> Items, PostAuthorResult Author);