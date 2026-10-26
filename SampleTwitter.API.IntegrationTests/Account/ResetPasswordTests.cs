using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SampleTwitter.API.Abstractions;
using SampleTwitter.API.Data;
using SampleTwitter.API.DTOs.RequestDTOs;
using SampleTwitter.API.DTOs.ResponseDTOs;
using SampleTwitter.API.IntegrationTests.Infrastructure;
using SampleTwitter.API.Models;

namespace SampleTwitter.API.IntegrationTests.Account;

public class ResetPasswordTests : IntegrationTestBase
{
    public ResetPasswordTests(ApiWebApplicationFactory factory) : base(factory) { }

    public static TheoryData<object, string> InvalidRequestCases => new()
    {
        { new { UserId = 1L, NewPassword = "NewPassword1!" },                         "Token"       },
        { new { UserId = 1L, Token = "", NewPassword = "NewPassword1!" },             "Token"       },
        { new { UserId = 1L, Token = "   ", NewPassword = "NewPassword1!" },          "Token"       },
        { new { UserId = 1L, Token = "valid-token" },                                 "NewPassword" },
        { new { UserId = 1L, Token = "valid-token", NewPassword = "" },               "NewPassword" },
        { new { UserId = 1L, Token = "valid-token", NewPassword = "short" },          "NewPassword" },
        { new { UserId = 1L, Token = "valid-token", NewPassword = "1234567" },        "NewPassword" }
    };

    [Theory]
    [MemberData(nameof(InvalidRequestCases))]
    public async Task InvalidRequest_Returns400WithValidationProblemDetailsNamingOffendingField(
        object requestBody, string expectedInvalidField)
    {
        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/reset-password", requestBody);

        // Assert
        await response.AssertValidationProblemDetails(expectedInvalidField);
    }

    [Fact]
    public async Task ValidToken_Returns200WithResetPasswordResponse()
    {
        // Arrange
        var (user, rawToken) = await SeedUserWithValidResetToken("user@example.com", "OldPassword1!");

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest
            {
                UserId = user.Id,
                Token = rawToken,
                NewPassword = "NewPassword1!"
            });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ResetPasswordResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.Message));
    }

    [Fact]
    public async Task ValidToken_UpdatesPasswordHashAndUpdatedAtAndMarksTokenUsed()
    {
        // Arrange
        var (user, rawToken) = await SeedUserWithValidResetToken("persist@example.com", "OldPassword1!");

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest
            {
                UserId = user.Id,
                Token = rawToken,
                NewPassword = "NewPassword1!"
            });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var updatedUser = await db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        Assert.NotEqual(user.PasswordHash, updatedUser.PasswordHash);
        Assert.NotEqual("NewPassword1!", updatedUser.PasswordHash);
        Assert.True(hasher.Verify("NewPassword1!", updatedUser.PasswordHash));
        Assert.NotNull(updatedUser.UpdatedAt);

        var tokenEntity = await db.PasswordResetTokens.AsNoTracking().SingleAsync(t => t.UserId == user.Id);
        Assert.NotNull(tokenEntity.UsedAt);
    }

    [Fact]
    public async Task ValidToken_AllowsLoginWithNewPasswordAndRejectsOldPassword()
    {
        // Arrange
        var (user, rawToken) = await SeedUserWithValidResetToken("logincheck@example.com", "OldPassword1!");

        // Act
        var resetResponse = await Client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest
            {
                UserId = user.Id,
                Token = rawToken,
                NewPassword = "UpdatedPassword1!"
            });
        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);

        // Assert — old password returns 401
        var oldLoginResponse = await Client.PostAsJsonAsync("/api/auth/signin",
            new LoginRequest { Email = "logincheck@example.com", Password = "OldPassword1!" });
        await oldLoginResponse.AssertProblemDetails(HttpStatusCode.Unauthorized);

        // Assert — new password returns 200
        var newLoginResponse = await Client.PostAsJsonAsync("/api/auth/signin",
            new LoginRequest { Email = "logincheck@example.com", Password = "UpdatedPassword1!" });
        Assert.Equal(HttpStatusCode.OK, newLoginResponse.StatusCode);
    }

    [Fact]
    public async Task ValidToken_Exactly8CharacterPassword_Returns200AndUpdatesPassword()
    {
        // Arrange
        var (user, rawToken) = await SeedUserWithValidResetToken("boundary@example.com", "OldPassword1!");

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest
            {
                UserId = user.Id,
                Token = rawToken,
                NewPassword = "12345678"
            });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var loginResponse = await Client.PostAsJsonAsync("/api/auth/signin",
            new LoginRequest { Email = "boundary@example.com", Password = "12345678" });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
    }

    [Fact]
    public async Task ValidToken_DoesNotIssueAuthCookieDirectly()
    {
        // Arrange
        var (user, rawToken) = await SeedUserWithValidResetToken("nocookie@example.com", "OldPassword1!");

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest
            {
                UserId = user.Id,
                Token = rawToken,
                NewPassword = "NewPassword1!"
            });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(
            response.Headers.TryGetValues("Set-Cookie", out var cookieValues) &&
            cookieValues.Any(v => v.StartsWith("SampleTwitter.Auth=")));
    }

    [Fact]
    public async Task ValidToken_PreservesOtherUserFields()
    {
        // Arrange
        var (user, rawToken) = await SeedUserWithValidResetToken("preserve@example.com", "OldPassword1!");

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
            var dbUser = await db.Users.SingleAsync(u => u.Id == user.Id);
            dbUser.UpdateProfile("KeptUsername", "https://example.com/kept.png");
            await db.SaveChangesAsync();
        }

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest
            {
                UserId = user.Id,
                Token = rawToken,
                NewPassword = "NewPassword1!"
            });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
            var updated = await db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
            Assert.Equal("preserve@example.com", updated.Email);
            Assert.Equal("KeptUsername", updated.Username);
            Assert.Equal("https://example.com/kept.png", updated.ImageUrl);
            Assert.True(updated.EmailConfirmed);
            Assert.Equal(user.RegisteredAt.ToUnixTimeMilliseconds(), updated.RegisteredAt.ToUnixTimeMilliseconds());
        }
    }

    [Fact]
    public async Task WrongTokenString_Returns400AndDoesNotMutatePasswordOrMarkTokenUsed()
    {
        // Arrange
        var (user, _) = await SeedUserWithValidResetToken("wrongtoken@example.com", "OldPassword1!");

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest
            {
                UserId = user.Id,
                Token = "completely-wrong-token-string",
                NewPassword = "AttemptedPassword1!"
            });

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.BadRequest);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

        var unchangedUser = await db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        Assert.Equal(user.PasswordHash, unchangedUser.PasswordHash);
        Assert.Null(unchangedUser.UpdatedAt);

        var tokenEntity = await db.PasswordResetTokens.AsNoTracking().SingleAsync(t => t.UserId == user.Id);
        Assert.Null(tokenEntity.UsedAt);
    }

    [Fact]
    public async Task NonExistentUserId_Returns400WithProblemDetails()
    {
        // Arrange
        var (user, rawToken) = await SeedUserWithValidResetToken("user@example.com", "OldPassword1!");

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest
            {
                UserId = user.Id + 99999,
                Token = rawToken,
                NewPassword = "NewPassword1!"
            });

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ExpiredToken_Returns400AndDoesNotMutatePassword()
    {
        // Arrange
        var (user, rawToken) = await SeedUserWithResetToken(
            "expired@example.com",
            "OldPassword1!",
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(-10),
            usedAt: null);

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest
            {
                UserId = user.Id,
                Token = rawToken,
                NewPassword = "NewPassword1!"
            });

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.BadRequest);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var unchangedUser = await db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        Assert.Equal(user.PasswordHash, unchangedUser.PasswordHash);
        Assert.Null(unchangedUser.UpdatedAt);
    }

    [Fact]
    public async Task AlreadyUsedToken_Returns400AndDoesNotMutatePassword()
    {
        // Arrange
        var (user, rawToken) = await SeedUserWithResetToken(
            "used@example.com",
            "OldPassword1!",
            expiresAt: DateTimeOffset.UtcNow.AddHours(1),
            usedAt: DateTimeOffset.UtcNow.AddMinutes(-5));

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest
            {
                UserId = user.Id,
                Token = rawToken,
                NewPassword = "NewPassword1!"
            });

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.BadRequest);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var unchangedUser = await db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        Assert.Equal(user.PasswordHash, unchangedUser.PasswordHash);
    }

    [Fact]
    public async Task ValidToken_UsedTwice_SecondCallReturns400AndDoesNotChangePasswordAgain()
    {
        // Arrange
        var (user, rawToken) = await SeedUserWithValidResetToken("replay@example.com", "OldPassword1!");

        // Act 1 — first reset succeeds
        var firstResponse = await Client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest
            {
                UserId = user.Id,
                Token = rawToken,
                NewPassword = "FirstNewPassword1!"
            });
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        // Act 2 — replay with the same token
        var secondResponse = await Client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest
            {
                UserId = user.Id,
                Token = rawToken,
                NewPassword = "SecondReplayPassword1!"
            });

        // Assert
        await secondResponse.AssertProblemDetails(HttpStatusCode.BadRequest);

        // First new password still works; replayed password does not
        var firstLogin = await Client.PostAsJsonAsync("/api/auth/signin",
            new LoginRequest { Email = "replay@example.com", Password = "FirstNewPassword1!" });
        Assert.Equal(HttpStatusCode.OK, firstLogin.StatusCode);

        var secondLogin = await Client.PostAsJsonAsync("/api/auth/signin",
            new LoginRequest { Email = "replay@example.com", Password = "SecondReplayPassword1!" });
        Assert.Equal(HttpStatusCode.Unauthorized, secondLogin.StatusCode);
    }

    [Fact]
    public async Task CrossUser_TokenBelongsToDifferentUser_Returns400AndDoesNotMutateEitherUser()
    {
        // Arrange
        var (userA, _) = await SeedUserWithValidResetToken("usera@example.com", "UserAPassword1!");
        var (userB, rawTokenB) = await SeedUserWithValidResetToken("userb@example.com", "UserBPassword1!");

        // Act — attempt to reset userA's password using userB's token
        var response = await Client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest
            {
                UserId = userA.Id,
                Token = rawTokenB,
                NewPassword = "HackedPassword1!"
            });

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.BadRequest);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

        var dbUserA = await db.Users.AsNoTracking().SingleAsync(u => u.Id == userA.Id);
        var dbUserB = await db.Users.AsNoTracking().SingleAsync(u => u.Id == userB.Id);
        Assert.Equal(userA.PasswordHash, dbUserA.PasswordHash);
        Assert.Equal(userB.PasswordHash, dbUserB.PasswordHash);

        var tokenB = await db.PasswordResetTokens.AsNoTracking().SingleAsync(t => t.UserId == userB.Id);
        Assert.Null(tokenB.UsedAt);
    }

    [Fact]
    public async Task FailedValidation_ShortPassword_DoesNotConsumeTokenOrMutatePassword()
    {
        // Arrange
        var (user, rawToken) = await SeedUserWithValidResetToken("retry@example.com", "OldPassword1!");

        // Act 1 — invalid short password
        var failedResponse = await Client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest
            {
                UserId = user.Id,
                Token = rawToken,
                NewPassword = "short"
            });
        await failedResponse.AssertValidationProblemDetails("NewPassword");

        // Act 2 — retry with the same token and a valid password
        var retryResponse = await Client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest
            {
                UserId = user.Id,
                Token = rawToken,
                NewPassword = "ValidNewPassword1!"
            });

        // Assert — token was not consumed by the failed validation request, so retry succeeds
        Assert.Equal(HttpStatusCode.OK, retryResponse.StatusCode);
    }

    [Fact]
    public async Task TokenInvalidatedByAuthenticatedChangePassword_Returns400()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("changepw@example.com", "OldPassword1!");
        var rawToken = $"reset-token-{Guid.NewGuid():N}";

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
            db.PasswordResetTokens.Add(PasswordResetToken.Create(
                user.Id,
                ComputeTokenHash(rawToken)));
            await db.SaveChangesAsync();
        }

        // User changes password while logged in
        var changePwMessage = new HttpRequestMessage(HttpMethod.Put, "/api/auth/password")
        {
            Content = JsonContent.Create(new ChangePasswordRequest
            {
                CurrentPassword = "OldPassword1!",
                NewPassword = "ChangedPassword1!"
            }),
            Headers = { { "Cookie", cookie } }
        };
        var changePwResponse = await Client.SendAsync(changePwMessage);
        Assert.Equal(HttpStatusCode.OK, changePwResponse.StatusCode);

        // Act — try to use the pre-existing reset token
        var resetResponse = await Client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest
            {
                UserId = user.Id,
                Token = rawToken,
                NewPassword = "ResetPasswordAttempt1!"
            });

        // Assert — reset token was invalidated by ChangePassword
        await resetResponse.AssertProblemDetails(HttpStatusCode.BadRequest);
    }

    private Task<(User User, string RawToken)> SeedUserWithValidResetToken(string email, string password)
    {
        return SeedUserWithResetToken(
            email,
            password,
            expiresAt: DateTimeOffset.UtcNow.AddHours(1),
            usedAt: null);
    }

    private async Task<(User User, string RawToken)> SeedUserWithResetToken(
        string email,
        string password,
        DateTimeOffset expiresAt,
        DateTimeOffset? usedAt)
    {
        var user = await SeedUser(email, password, emailConfirmed: true);
        var rawToken = $"integration-test-reset-token-{Guid.NewGuid():N}";

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

        var createdAt = expiresAt.Subtract(PasswordResetToken.DefaultLifetime);
        var token = PasswordResetToken.Create(user.Id, ComputeTokenHash(rawToken), createdAt);
        if (usedAt is not null)
        {
            token.MarkUsed(usedAt);
        }

        db.PasswordResetTokens.Add(token);
        await db.SaveChangesAsync();

        return (user, rawToken);
    }

    private string ComputeTokenHash(string rawToken)
    {
        using var scope = Factory.Services.CreateScope();
        var tokenGenerator = scope.ServiceProvider.GetRequiredService<ISecureTokenGenerator>();
        return tokenGenerator.Hash(rawToken);
    }
}