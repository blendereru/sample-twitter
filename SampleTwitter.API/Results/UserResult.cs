namespace SampleTwitter.API.Results;

public record UserResult(long Id, string Email, DateTimeOffset RegisteredAt);