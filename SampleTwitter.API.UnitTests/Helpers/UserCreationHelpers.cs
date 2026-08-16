using SampleTwitter.API.Models;

namespace SampleTwitter.API.UnitTests.Helpers;

public static class UserCreationHelpers
{
    public static User CreateUser(long id = 1, string email = "user@example.com") => new()
    {
        Id = id,
        Email = email,
        PasswordHash = "hash",
        EmailConfirmed = false,
        RegisteredAt = DateTimeOffset.UtcNow
    };
}