using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SampleTwitter.API.Abstractions;
using SampleTwitter.API.Data;
using SampleTwitter.API.DTOs.RequestDTOs;
using SampleTwitter.API.DTOs.ResponseDTOs;
using SampleTwitter.API.IntegrationTests.Infrastructure;
using SampleTwitter.API.Models;

namespace SampleTwitter.API.IntegrationTests.Account;

public class ForgotPasswordTests : IntegrationTestBase
{
    public ForgotPasswordTests(ApiWebApplicationFactory factory) : base(factory) { }

    public static TheoryData<object, string> InvalidRequestCases => new()
    {
        { new { },                                                    "Email" },
        { new { Email = "" },                                         "Email" },
        { new { Email = "   " },                                      "Email" },
        { new { Email = "not-an-email" },                             "Email" },
        { new ForgotPasswordRequest { Email = "missing-at-sign.com" }, "Email" }
    };

    [Theory]
    [MemberData(nameof(InvalidRequestCases))]
    public async Task InvalidRequest_Returns400WithValidationProblemDetailsNamingEmail(
        object requestBody, string expectedInvalidField)
    {
        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/forgot-password", requestBody);

        // Assert
        await response.AssertValidationProblemDetails(expectedInvalidField);
        Assert.Empty(Factory.FakeEmailSender.SentEmails);
    }

    [Fact]
    public async Task ConfirmedUser_Returns200WithForgotPasswordResponseAndSendsResetEmail()
    {
        // Arrange
        var user = await SeedUser("alice@example.com", "Sup3rSecret1!", emailConfirmed: true);

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest { Email = "alice@example.com" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ForgotPasswordResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.Message));

        var sentEmail = Assert.Single(Factory.FakeEmailSender.SentEmails);
        Assert.Equal("alice@example.com", sentEmail.ToEmail);
        Assert.Contains($"/reset-password?userId={user.Id}&token=", sentEmail.HtmlBody);
    }

    [Fact]
    public async Task ConfirmedUser_PersistsHashedPasswordResetTokenInDatabase()
    {
        // Arrange
        var user = await SeedUser("token_user@example.com", "Sup3rSecret1!", emailConfirmed: true);

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest { Email = "token_user@example.com" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var sentEmail = Assert.Single(Factory.FakeEmailSender.SentEmails);
        var (_, rawToken) = ExtractResetParams(sentEmail.HtmlBody);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var tokenGenerator = scope.ServiceProvider.GetRequiredService<ISecureTokenGenerator>();

        var tokenEntity = await db.PasswordResetTokens
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.UserId == user.Id);

        Assert.NotNull(tokenEntity);
        Assert.NotEqual(rawToken, tokenEntity.TokenHash);
        Assert.Equal(tokenGenerator.Hash(rawToken), tokenEntity.TokenHash);
        Assert.Null(tokenEntity.UsedAt);
        Assert.True(tokenEntity.ExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task ConfirmedUser_EmailWithWhitespaceAndMixedCase_NormalizesAndSendsResetEmail()
    {
        // Arrange
        var user = await SeedUser("normalized@example.com", "Sup3rSecret1!", emailConfirmed: true);

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest { Email = "  Normalized@Example.COM  " });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var sentEmail = Assert.Single(Factory.FakeEmailSender.SentEmails);
        Assert.Equal("normalized@example.com", sentEmail.ToEmail);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var tokenExists = await db.PasswordResetTokens.AnyAsync(t => t.UserId == user.Id);
        Assert.True(tokenExists);
    }

    [Fact]
    public async Task ConfirmedUser_DoesNotIssueAuthCookieOrMutateCurrentPassword()
    {
        // Arrange
        var user = await SeedUser("unchanged_pw@example.com", "Sup3rSecret1!", emailConfirmed: true);

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest { Email = "unchanged_pw@example.com" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(
            response.Headers.TryGetValues("Set-Cookie", out var cookieValues) &&
            cookieValues.Any(v => v.StartsWith("SampleTwitter.Auth=")));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var dbUser = await db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        Assert.Equal(user.PasswordHash, dbUser.PasswordHash);

        // User can still log in with the original password before resetting
        var loginResponse = await Client.PostAsJsonAsync("/api/auth/signin",
            new LoginRequest { Email = "unchanged_pw@example.com", Password = "Sup3rSecret1!" });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
    }

    [Fact]
    public async Task NonExistentEmail_Returns200WithSameMessageAndDoesNotSendEmailOrCreateToken()
    {
        // Arrange
        await SeedUser("existing@example.com", "Sup3rSecret1!", emailConfirmed: true);

        var existingResponse = await Client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest { Email = "existing@example.com" });
        var existingBody = await existingResponse.Content.ReadFromJsonAsync<ForgotPasswordResponse>();
        Factory.FakeEmailSender.Clear();

        // Act
        var nonExistentResponse = await Client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest { Email = "nonexistent@example.com" });

        // Assert — anti-enumeration: identical 200 status and message
        Assert.Equal(HttpStatusCode.OK, nonExistentResponse.StatusCode);
        var nonExistentBody = await nonExistentResponse.Content.ReadFromJsonAsync<ForgotPasswordResponse>();
        Assert.NotNull(nonExistentBody);
        Assert.Equal(existingBody!.Message, nonExistentBody.Message);

        // Assert — no email sent
        Assert.Empty(Factory.FakeEmailSender.SentEmails);
    }

    [Fact]
    public async Task UnconfirmedUser_Returns200WithSameMessageAndDoesNotSendEmailOrCreateToken()
    {
        // Arrange
        var confirmedUser = await SeedUser("confirmed@example.com", "Sup3rSecret1!", emailConfirmed: true);
        var unconfirmedUser = await SeedUser("unconfirmed@example.com", "Sup3rSecret1!", emailConfirmed: false);

        var confirmedResponse = await Client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest { Email = confirmedUser.Email });
        var confirmedBody = await confirmedResponse.Content.ReadFromJsonAsync<ForgotPasswordResponse>();
        Factory.FakeEmailSender.Clear();

        // Act
        var unconfirmedResponse = await Client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest { Email = unconfirmedUser.Email });

        // Assert — anti-enumeration: identical 200 status and message
        Assert.Equal(HttpStatusCode.OK, unconfirmedResponse.StatusCode);
        var unconfirmedBody = await unconfirmedResponse.Content.ReadFromJsonAsync<ForgotPasswordResponse>();
        Assert.NotNull(unconfirmedBody);
        Assert.Equal(confirmedBody!.Message, unconfirmedBody.Message);

        // Assert — no email sent and no token persisted for unconfirmed user
        Assert.Empty(Factory.FakeEmailSender.SentEmails);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var tokenExists = await db.PasswordResetTokens.AnyAsync(t => t.UserId == unconfirmedUser.Id);
        Assert.False(tokenExists);
    }

    [Fact]
    public async Task MultipleRequests_InvalidatesPreviousUnusedTokensAndKeepsOnlyLatestActive()
    {
        // Arrange
        var user = await SeedUser("repeat@example.com", "Sup3rSecret1!", emailConfirmed: true);

        await Client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest { Email = "repeat@example.com" });
        var firstEmail = Assert.Single(Factory.FakeEmailSender.SentEmails);
        var (_, firstRawToken) = ExtractResetParams(firstEmail.HtmlBody);
        Factory.FakeEmailSender.Clear();

        // Act — request a second password reset
        var secondResponse = await Client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest { Email = "repeat@example.com" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var secondEmail = Assert.Single(Factory.FakeEmailSender.SentEmails);
        var (_, secondRawToken) = ExtractResetParams(secondEmail.HtmlBody);

        // Only 1 active token remains in the DB
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
            var tokens = await db.PasswordResetTokens.Where(t => t.UserId == user.Id).ToListAsync();
            Assert.Single(tokens);
        }

        // First token is rejected when attempting to reset password
        var firstResetAttempt = await Client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest
            {
                UserId = user.Id,
                Token = firstRawToken,
                NewPassword = "NewPassword123!"
            });
        await firstResetAttempt.AssertProblemDetails(HttpStatusCode.BadRequest);

        // Second token succeeds
        var secondResetAttempt = await Client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest
            {
                UserId = user.Id,
                Token = secondRawToken,
                NewPassword = "NewPassword123!"
            });
        Assert.Equal(HttpStatusCode.OK, secondResetAttempt.StatusCode);
    }

    [Fact]
    public async Task AlreadyUsedToken_IsPreservedWhenNewForgotPasswordRequested()
    {
        // Arrange
        var user = await SeedUser("history@example.com", "Sup3rSecret1!", emailConfirmed: true);

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
            db.PasswordResetTokens.Add(new PasswordResetToken
            {
                UserId = user.Id,
                TokenHash = "already-used-token-hash",
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-2),
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1),
                UsedAt = DateTimeOffset.UtcNow.AddMinutes(-90)
            });
            await db.SaveChangesAsync();
        }

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest { Email = "history@example.com" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
            var tokens = await db.PasswordResetTokens
                .AsNoTracking()
                .Where(t => t.UserId == user.Id)
                .ToListAsync();

            Assert.Equal(2, tokens.Count);
            Assert.Single(tokens, t => t.UsedAt is not null && t.TokenHash == "already-used-token-hash");
            Assert.Single(tokens, t => t.UsedAt is null);
        }
    }

    [Fact]
    public async Task MultipleUsers_OnlyInvalidatesTokensForTargetUser()
    {
        // Arrange
        var userA = await SeedUser("usera@example.com", "Sup3rSecret1!", emailConfirmed: true);
        var userB = await SeedUser("userb@example.com", "Sup3rSecret1!", emailConfirmed: true);

        await Client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest { Email = userA.Email });
        await Client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest { Email = userB.Email });

        var userBEmail = Factory.FakeEmailSender.SentEmails.Single(e => e.ToEmail == userB.Email);
        var (_, userBRawToken) = ExtractResetParams(userBEmail.HtmlBody);

        // Act — userA requests another password reset
        await Client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest { Email = userA.Email });

        // Assert — userB's token is still valid
        var userBResetResponse = await Client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest
            {
                UserId = userB.Id,
                Token = userBRawToken,
                NewPassword = "UserBNewPass1!"
            });
        Assert.Equal(HttpStatusCode.OK, userBResetResponse.StatusCode);
    }

    [Fact]
    public async Task ConfirmedUser_SentResetTokenCanResetPasswordAndSignInWithNewPassword()
    {
        // Arrange
        var user = await SeedUser("e2e@example.com", "OldPassword1!", emailConfirmed: true);

        // Act 1 — request forgot password
        var forgotResponse = await Client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest { Email = "e2e@example.com" });
        Assert.Equal(HttpStatusCode.OK, forgotResponse.StatusCode);

        var sentEmail = Assert.Single(Factory.FakeEmailSender.SentEmails);
        var (extractedUserId, rawToken) = ExtractResetParams(sentEmail.HtmlBody);
        Assert.Equal(user.Id, extractedUserId);

        // Act 2 — reset password using the extracted link parameters
        var resetResponse = await Client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest
            {
                UserId = extractedUserId,
                Token = rawToken,
                NewPassword = "BrandNewPassword1!"
            });
        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);

        // Assert — old password no longer works, new password succeeds
        var oldLogin = await Client.PostAsJsonAsync("/api/auth/signin",
            new LoginRequest { Email = "e2e@example.com", Password = "OldPassword1!" });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        var newLogin = await Client.PostAsJsonAsync("/api/auth/signin",
            new LoginRequest { Email = "e2e@example.com", Password = "BrandNewPassword1!" });
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    private static (long UserId, string RawToken) ExtractResetParams(string htmlBody)
    {
        var hrefStart = htmlBody.IndexOf("href=\"", StringComparison.Ordinal) + 6;
        var hrefEnd = htmlBody.IndexOf("\"", hrefStart, StringComparison.Ordinal);
        var fullUrl = htmlBody[hrefStart..hrefEnd].Replace("&amp;", "&");

        var uri = new Uri(fullUrl);
        var query = QueryHelpers.ParseQuery(uri.Query);

        var userId = long.Parse(query["userId"]!);
        var token = query["token"];
        return (userId, token)!;
    }
}