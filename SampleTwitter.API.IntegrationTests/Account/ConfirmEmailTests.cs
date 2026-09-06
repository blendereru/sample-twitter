using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SampleTwitter.API.Data;
using SampleTwitter.API.DTOs.RequestDTOs;
using SampleTwitter.API.DTOs.ResponseDTOs;
using SampleTwitter.API.IntegrationTests.Infrastructure;
using SampleTwitter.API.Models;

namespace SampleTwitter.API.IntegrationTests.Account;

public class ConfirmEmailTests : IntegrationTestBase
{
    private readonly HttpClient _rawClient;

    public ConfirmEmailTests(ApiWebApplicationFactory factory) : base(factory)
    {
        _rawClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
    }
    
    [Fact]
    public async Task ValidToken_Returns200AndSignsInAndMarksTokenUsed()
    {
        // Arrange
        var (userId, rawToken) = await SeedUserWithValidToken("user@example.com");

        // Act
        var response = await _rawClient.PostAsync(
            $"/api/account/confirm-email?userId={userId}&token={Uri.EscapeDataString(rawToken)}",
            content: null);

        // Assert — HTTP contract
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ConfirmEmailResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.Message));

        // Assert — auth cookie is issued (proves SignInAsync ran successfully)
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookieValues));
        Assert.Contains(cookieValues, v => v.StartsWith("SampleTwitter.Auth="));

        // Assert — DB: user is now confirmed
        var user = await QueryUserAsync("user@example.com");
        Assert.NotNull(user);
        Assert.True(user.EmailConfirmed);

        // Assert — DB: token is marked as used (replay protection)
        var token = await QueryTokenAsync(userId);
        Assert.NotNull(token);
        Assert.NotNull(token.UsedAt);
    }
    
    [Fact]
    public async Task TokenNotFound_WrongTokenString_Returns400WithProblemDetails()
    {
        // Arrange
        var (userId, _) = await SeedUserWithValidToken("user@example.com");

        // Act
        var response = await _rawClient.PostAsync(
            $"/api/account/confirm-email?userId={userId}&token=completely-wrong-token",
            content: null);

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task TokenNotFound_WrongUserId_Returns400WithProblemDetails()
    {
        // Arrange
        var (userId, rawToken) = await SeedUserWithValidToken("user@example.com");

        // Act
        var response = await _rawClient.PostAsync(
            $"/api/account/confirm-email?userId={userId + 999}&token={Uri.EscapeDataString(rawToken)}",
            content: null);

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task AlreadyUsedToken_Returns400WithProblemDetails()
    {
        // Arrange
        var (userId, rawToken) = await SeedUserWithUsedToken("user@example.com");

        // Act
        var response = await _rawClient.PostAsync(
            $"/api/account/confirm-email?userId={userId}&token={Uri.EscapeDataString(rawToken)}",
            content: null);

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task ExpiredToken_Returns400WithProblemDetails()
    {
        // Arrange
        var (userId, rawToken) = await SeedUserWithExpiredToken("user@example.com");

        // Act
        var response = await _rawClient.PostAsync(
            $"/api/account/confirm-email?userId={userId}&token={Uri.EscapeDataString(rawToken)}",
            content: null);

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task ValidToken_UsedTwice_SecondCallReturns400()
    {
        // Arrange
        var (userId, rawToken) = await SeedUserWithValidToken("user@example.com");
        var url = $"/api/account/confirm-email?userId={userId}&token={Uri.EscapeDataString(rawToken)}";

        // Act — first call
        var firstResponse = await _rawClient.PostAsync(url, content: null);

        // Assert — first call succeeds
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        // Act — second call with the same token
        var secondResponse = await _rawClient.PostAsync(url, content: null);

        // Assert — second call is rejected because UsedAt is now set
        await secondResponse.AssertProblemDetails(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CrossUser_TokenBelongsToDifferentUser_Returns400WithProblemDetails()
    {
        // Arrange
        var (userAId, _) = await SeedUserWithValidToken("usera@example.com");
        var (userBId, rawTokenB) = await SeedUserWithValidToken("userb@example.com");

        // Act
        var response = await _rawClient.PostAsync(
            $"/api/account/confirm-email?userId={userAId}&token={Uri.EscapeDataString(rawTokenB)}",
            content: null);

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.BadRequest);

        var userA = await QueryUserAsync("usera@example.com");
        var userB = await QueryUserAsync("userb@example.com");
        Assert.NotNull(userA);
        Assert.NotNull(userB);
        Assert.False(userA.EmailConfirmed);
        Assert.False(userB.EmailConfirmed);

        var tokenB = await QueryTokenAsync(userBId);
        Assert.NotNull(tokenB);
        Assert.Null(tokenB.UsedAt);
    }

    [Fact]
    public async Task FailedConfirmation_WrongToken_DoesNotConfirmUserOrMarkTokenUsedOrIssueCookie()
    {
        // Arrange
        var (userId, _) = await SeedUserWithValidToken("pending@example.com");

        // Act
        var response = await _rawClient.PostAsync(
            $"/api/account/confirm-email?userId={userId}&token=invalid-token",
            content: null);

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.BadRequest);

        Assert.False(
            response.Headers.TryGetValues("Set-Cookie", out var cookieValues) &&
            cookieValues.Any(v => v.StartsWith("SampleTwitter.Auth=")));

        var user = await QueryUserAsync("pending@example.com");
        Assert.NotNull(user);
        Assert.False(user.EmailConfirmed);

        var token = await QueryTokenAsync(userId);
        Assert.NotNull(token);
        Assert.Null(token.UsedAt);
    }

    [Fact]
    public async Task ValidToken_IssuedCookieCanAccessProtectedEndpoints()
    {
        // Arrange
        var (userId, rawToken) = await SeedUserWithValidToken("confirmed@example.com");

        // Act
        var confirmResponse = await _rawClient.PostAsync(
            $"/api/account/confirm-email?userId={userId}&token={Uri.EscapeDataString(rawToken)}",
            content: null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        Assert.True(confirmResponse.Headers.TryGetValues("Set-Cookie", out var cookieValues));
        var authCookie = cookieValues.First(v => v.StartsWith("SampleTwitter.Auth=")).Split(';')[0];

        // Act
        var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/account/me")
        {
            Headers = { { "Cookie", authCookie } }
        };
        var meResponse = await _rawClient.SendAsync(meRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        var meBody = await meResponse.Content.ReadFromJsonAsync<MeResponse>();
        Assert.NotNull(meBody);
        Assert.Equal(userId, meBody.Id);
        Assert.Equal("confirmed@example.com", meBody.Email);
    }

    [Fact]
    public async Task EmptyToken_Returns400WithProblemDetails()
    {
        // Arrange
        var (userId, _) = await SeedUserWithValidToken("user@example.com");

        // Act
        var response = await _rawClient.PostAsync(
            $"/api/account/confirm-email?userId={userId}&token=",
            content: null);

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SupersededToken_AfterResend_Returns400AndNewTokenReturns200()
    {
        // Arrange
        await Client.PostAsJsonAsync("/api/account/signup",
            new SignUpRequest { Email = "resend@example.com", Password = "Sup3rSecret1!" });

        var firstEmail = Assert.Single(Factory.FakeEmailSender.SentEmails);
        var firstConfirmUrl = ExtractConfirmationUrl(firstEmail.HtmlBody);
        Factory.FakeEmailSender.Clear();

        await Client.PostAsJsonAsync("/api/account/signup",
            new SignUpRequest { Email = "resend@example.com", Password = "Sup3rSecret1!" });

        var secondEmail = Assert.Single(Factory.FakeEmailSender.SentEmails);
        var secondConfirmUrl = ExtractConfirmationUrl(secondEmail.HtmlBody);

        // Act
        var firstResponse = await _rawClient.PostAsync(firstConfirmUrl, content: null);

        // Assert
        await firstResponse.AssertProblemDetails(HttpStatusCode.BadRequest);

        // Act
        var secondResponse = await _rawClient.PostAsync(secondConfirmUrl, content: null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var user = await QueryUserAsync("resend@example.com");
        Assert.NotNull(user);
        Assert.True(user.EmailConfirmed);
    }
    
    private async Task<(long UserId, string RawToken)> SeedUserWithValidToken(
        string email,
        string? rawToken = null)
    {
        rawToken ??= $"integration-test-raw-token-{Guid.NewGuid():N}";

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

        var user = new User
        {
            Email = email,
            PasswordHash = "some-hash",
            EmailConfirmed = false,
            RegisteredAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.EmailConfirmationTokens.Add(new EmailConfirmationToken
        {
            UserId = user.Id,
            TokenHash = ComputeTokenHash(rawToken),
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(24),
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        return (user.Id, rawToken);
    }
    
    private async Task<(long UserId, string RawToken)> SeedUserWithUsedToken(string email)
    {
        const string rawToken = "integration-test-used-token";

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

        var user = new User
        {
            Email = email,
            PasswordHash = "some-hash",
            EmailConfirmed = true,
            RegisteredAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.EmailConfirmationTokens.Add(new EmailConfirmationToken
        {
            UserId = user.Id,
            TokenHash = ComputeTokenHash(rawToken),
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(24),
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            UsedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        });
        await db.SaveChangesAsync();

        return (user.Id, rawToken);
    }
    
    private async Task<(long UserId, string RawToken)> SeedUserWithExpiredToken(string email)
    {
        const string rawToken = "integration-test-expired-token";

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

        var user = new User
        {
            Email = email,
            PasswordHash = "some-hash",
            EmailConfirmed = false,
            RegisteredAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.EmailConfirmationTokens.Add(new EmailConfirmationToken
        {
            UserId = user.Id,
            TokenHash = ComputeTokenHash(rawToken),
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1),
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-25)
        });
        await db.SaveChangesAsync();

        return (user.Id, rawToken);
    }

    private async Task<User?> QueryUserAsync(string email)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        return await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Email == email);
    }

    private async Task<EmailConfirmationToken?> QueryTokenAsync(long userId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        return await db.EmailConfirmationTokens.AsNoTracking()
            .SingleOrDefaultAsync(t => t.UserId == userId);
    }
    
    private static string ComputeTokenHash(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes);
    }

    private static string ExtractConfirmationUrl(string htmlBody)
    {
        var hrefStart = htmlBody.IndexOf("href=\"", StringComparison.Ordinal) + 6;
        var hrefEnd = htmlBody.IndexOf("\"", hrefStart, StringComparison.Ordinal);
        var fullUrl = htmlBody[hrefStart..hrefEnd];

        var uri = new Uri(fullUrl.Replace("&amp;", "&"));
        return $"/api/account/confirm-email{uri.Query}";
    }
}