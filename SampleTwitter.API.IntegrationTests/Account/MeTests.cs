using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SampleTwitter.API.Abstractions;
using SampleTwitter.API.Data;
using SampleTwitter.API.DTOs.RequestDTOs;
using SampleTwitter.API.DTOs.ResponseDTOs;
using SampleTwitter.API.IntegrationTests.Infrastructure;
using SampleTwitter.API.Models;

namespace SampleTwitter.API.IntegrationTests.Account;

public class MeTests : IntegrationTestBase
{
    public MeTests(ApiWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Authenticated_Returns200WithCorrectUserInfo()
    {
        // Arrange
        var password = "Sup3rSecret1!";
        await SeedConfirmedUser("user@example.com", password);

        // Act
        await Client.PostAsJsonAsync("/api/account/signin",
            new LoginRequest { Email = "user@example.com", Password = password });

        var response = await Client.GetAsync("/api/account/me");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<MeResponse>();
        Assert.NotNull(body);
        Assert.True(body.Id > 0);
        Assert.Equal("user@example.com", body.Email);
        Assert.True(body.RegisteredAt < DateTimeOffset.UtcNow);
        Assert.True(body.RegisteredAt > DateTimeOffset.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        // Act
        var response = await Client.GetAsync("/api/account/me");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AfterLogin_MeReturnsTheSameUserIdAsLoginResponse()
    {
        // Arrange
        var password = "Sup3rSecret1!";
        await SeedConfirmedUser("user@example.com", password);

        // Act
        var loginResponse = await Client.PostAsJsonAsync("/api/account/signin",
            new LoginRequest { Email = "user@example.com", Password = password });
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        var meResponse = await Client.GetAsync("/api/account/me");
        var meBody = await meResponse.Content.ReadFromJsonAsync<MeResponse>();

        // Assert
        Assert.NotNull(loginBody);
        Assert.NotNull(meBody);
        Assert.Equal(loginBody.UserId, meBody.Id);
        Assert.Equal(loginBody.Email, meBody.Email);
    }

    [Fact]
    public async Task AfterConfirmEmail_MeReturnsTheConfirmedUser()
    {
        // Arrange
        await Client.PostAsJsonAsync("/api/account/signup",
            new SignUpRequest { Email = "confirm-me@example.com", Password = "Sup3rSecret1!" });
        
        var sentEmail = Assert.Single(Factory.FakeEmailSender.SentEmails);
        var confirmUrl = ExtractConfirmationUrl(sentEmail.HtmlBody);

        // Act
        var confirmResponse = await Client.PostAsync(confirmUrl, content: null);
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        
        var meResponse = await Client.GetAsync("/api/account/me");

        // Assert
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        var meBody = await meResponse.Content.ReadFromJsonAsync<MeResponse>();
        Assert.NotNull(meBody);
        Assert.Equal("confirm-me@example.com", meBody.Email);
    }

    [Fact]
    public async Task MeResponseDoesNotContainSensitiveFields()
    {
        // Arrange
        var password = "Sup3rSecret1!";
        await SeedConfirmedUser("user@example.com", password);

        await Client.PostAsJsonAsync("/api/account/signin",
            new LoginRequest { Email = "user@example.com", Password = password });

        // Act
        var response = await Client.GetAsync("/api/account/me");
        var rawJson = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.DoesNotContain("passwordHash", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", rawJson, StringComparison.OrdinalIgnoreCase);
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

    private static string ExtractConfirmationUrl(string htmlBody)
    {
        var hrefStart = htmlBody.IndexOf("href=\"", StringComparison.Ordinal) + 6;
        var hrefEnd = htmlBody.IndexOf("\"", hrefStart, StringComparison.Ordinal);
        var fullUrl = htmlBody[hrefStart..hrefEnd];

        var uri = new Uri(fullUrl.Replace("&amp;", "&"));
        return $"/api/account/confirm-email{uri.Query}";
    }
}
