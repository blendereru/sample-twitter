using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SampleTwitter.API.Abstractions;
using SampleTwitter.API.Data;
using SampleTwitter.API.DTOs.RequestDTOs;
using SampleTwitter.API.DTOs.ResponseDTOs;
using SampleTwitter.API.IntegrationTests.Infrastructure;
using SampleTwitter.API.Models;

namespace SampleTwitter.API.IntegrationTests.Account;

public class LoginTests : IntegrationTestBase
{
    private readonly HttpClient _rawClient;

    public LoginTests(ApiWebApplicationFactory factory) : base(factory)
    {
        _rawClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
    }

    [Fact]
    public async Task ValidCredentials_Returns200WithLoginResponseAndIssuesAuthCookie()
    {
        // Arrange
        await SeedConfirmedUser("user@example.com", "Sup3rSecret1!");

        // Act
        var response = await _rawClient.PostAsJsonAsync("/api/account/signin",
            new LoginRequest { Email = "user@example.com", Password = "Sup3rSecret1!" });

        // Assert — HTTP contract
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        Assert.True(body.UserId > 0);
        Assert.Equal("user@example.com", body.Email);
        Assert.False(string.IsNullOrWhiteSpace(body.Message));

        // Assert — auth cookie is issued
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookieValues));
        Assert.Contains(cookieValues, v => v.StartsWith("SampleTwitter.Auth="));
    }

    public static TheoryData<object, string> InvalidRequestCases => new()
    {
        { new { Password = "Sup3rSecret1!" },        "Email" },
        { new { Email = "user@example.com" },         "Password" },
        { new { Email = "not-an-email", Password = "Sup3rSecret1!" }, "Email" },
        { new { Email = "", Password = "Sup3rSecret1!" }, "Email" },
        { new { Email = "user@example.com", Password = "" }, "Password" }
    };

    [Theory]
    [MemberData(nameof(InvalidRequestCases))]
    public async Task InvalidRequest_Returns400WithValidationProblemDetailsNamingTheOffendingField(
        object requestBody, string expectedInvalidField)
    {
        // Arrange - requestBody provided by MemberData

        // Act
        var response = await Client.PostAsJsonAsync("/api/account/signin", requestBody);

        // Assert
        await response.AssertValidationProblemDetails(expectedInvalidField);
    }

    [Fact]
    public async Task NonExistentUser_Returns401WithProblemDetails()
    {
        // Arrange — no users seeded

        // Act
        var response = await Client.PostAsJsonAsync("/api/account/signin",
            new LoginRequest { Email = "nobody@example.com", Password = "Sup3rSecret1!" });

        // Assert
        var problemDetails = await response.AssertProblemDetails(HttpStatusCode.Unauthorized);
        Assert.DoesNotContain("nobody@example.com", problemDetails.Detail ?? string.Empty);
    }

    [Fact]
    public async Task UnconfirmedUser_Returns403WithProblemDetails()
    {
        // Arrange
        await SeedUnconfirmedUser("pending@example.com", "Sup3rSecret1!");

        // Act
        var response = await Client.PostAsJsonAsync("/api/account/signin",
            new LoginRequest { Email = "pending@example.com", Password = "Sup3rSecret1!" });

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task WrongPassword_Returns401WithProblemDetails()
    {
        // Arrange
        await SeedConfirmedUser("user@example.com", "CorrectPassword1!");

        // Act
        var response = await Client.PostAsJsonAsync("/api/account/signin",
            new LoginRequest { Email = "user@example.com", Password = "WrongPassword1!" });

        // Assert
        var problemDetails = await response.AssertProblemDetails(HttpStatusCode.Unauthorized);
        Assert.DoesNotContain("user@example.com", problemDetails.Detail ?? string.Empty);
    }

    [Fact]
    public async Task EmailWithWhitespaceAndMixedCase_NormalizesAndReturns200()
    {
        // Arrange
        await SeedConfirmedUser("user@example.com", "Sup3rSecret1!");

        // Act
        var response = await Client.PostAsJsonAsync("/api/account/signin",
            new LoginRequest { Email = "  USER@EXAMPLE.COM  ", Password = "Sup3rSecret1!" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        Assert.Equal("user@example.com", body.Email);
    }

    [Fact]
    public async Task WrongPassword_ErrorMessageDoesNotRevealThatUserExists()
    {
        // Arrange
        await SeedConfirmedUser("user@example.com", "CorrectPassword1!");

        // Act
        var response = await Client.PostAsJsonAsync("/api/account/signin",
            new LoginRequest { Email = "user@example.com", Password = "WrongPassword1!" });

        // Assert — the public detail should be the same generic message for wrong password
        // and non-existent user, preventing user enumeration
        var wrongPwDetails = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();

        var nonExistentResponse = await Client.PostAsJsonAsync("/api/account/signin",
            new LoginRequest { Email = "noone@example.com", Password = "Whatever1!" });
        var nonExistentDetails = await nonExistentResponse.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();

        Assert.Equal(wrongPwDetails?.Detail, nonExistentDetails?.Detail);
    }

    [Fact]
    public async Task FailedLogin_WrongPassword_DoesNotIssueAuthCookie()
    {
        // Arrange
        await SeedConfirmedUser("user@example.com", "CorrectPassword1!");

        // Act
        var response = await _rawClient.PostAsJsonAsync("/api/account/signin",
            new LoginRequest { Email = "user@example.com", Password = "WrongPassword1!" });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(
            response.Headers.TryGetValues("Set-Cookie", out var cookieValues) &&
            cookieValues.Any(v => v.StartsWith("SampleTwitter.Auth=")));
    }

    [Fact]
    public async Task FailedLogin_UnconfirmedUser_DoesNotIssueAuthCookie()
    {
        // Arrange
        await SeedUnconfirmedUser("pending@example.com", "Sup3rSecret1!");

        // Act
        var response = await _rawClient.PostAsJsonAsync("/api/account/signin",
            new LoginRequest { Email = "pending@example.com", Password = "Sup3rSecret1!" });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(
            response.Headers.TryGetValues("Set-Cookie", out var cookieValues) &&
            cookieValues.Any(v => v.StartsWith("SampleTwitter.Auth=")));
    }

    [Fact]
    public async Task ValidCredentials_IssuedCookieHasSecurityAttributes()
    {
        // Arrange
        await SeedConfirmedUser("user@example.com", "Sup3rSecret1!");

        // Act
        var response = await _rawClient.PostAsJsonAsync("/api/account/signin",
            new LoginRequest { Email = "user@example.com", Password = "Sup3rSecret1!" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookieValues));

        var authCookieHeader = cookieValues.First(v => v.StartsWith("SampleTwitter.Auth="));
        Assert.Contains("httponly", authCookieHeader, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", authCookieHeader, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidCredentials_IssuedCookieCanAccessProtectedEndpoints()
    {
        // Arrange
        await SeedConfirmedUser("user@example.com", "Sup3rSecret1!");

        // Act
        var loginResponse = await _rawClient.PostAsJsonAsync("/api/account/signin",
            new LoginRequest { Email = "user@example.com", Password = "Sup3rSecret1!" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.True(loginResponse.Headers.TryGetValues("Set-Cookie", out var cookieValues));
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
        Assert.Equal("user@example.com", meBody.Email);
    }

    [Fact]
    public async Task ValidCredentials_MultipleUsersInDatabase_AuthenticatesCorrectUser()
    {
        // Arrange
        await SeedConfirmedUser("alice@example.com", "PasswordAlice1!");
        await SeedConfirmedUser("bob@example.com", "PasswordBob1!");

        // Act
        var response = await _rawClient.PostAsJsonAsync("/api/account/signin",
            new LoginRequest { Email = "bob@example.com", Password = "PasswordBob1!" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        Assert.Equal("bob@example.com", body.Email);
    }

    private async Task SeedConfirmedUser(string email, string password)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        db.Users.Add(new User
        {
            Email = email,
            PasswordHash = hasher.Hash(password),
            EmailConfirmed = true,
            RegisteredAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private async Task SeedUnconfirmedUser(string email, string password)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        db.Users.Add(new User
        {
            Email = email,
            PasswordHash = hasher.Hash(password),
            EmailConfirmed = false,
            RegisteredAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }
}