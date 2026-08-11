namespace SampleTwitter.API.Results;

public record RegisterResult(long UserId, string Email, bool IsNewRegistration);