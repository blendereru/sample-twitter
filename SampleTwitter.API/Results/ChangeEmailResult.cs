namespace SampleTwitter.API.Results;

public record ChangeEmailResult(
    long UserId,
    string Email,
    DateTimeOffset? UpdatedAt
);
