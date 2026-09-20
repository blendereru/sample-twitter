using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SampleTwitter.API.Abstractions;
using SampleTwitter.API.Data;
using SampleTwitter.API.DTOs.RequestDTOs;
using SampleTwitter.API.DTOs.ResponseDTOs;
using SampleTwitter.API.IntegrationTests.Infrastructure;
using SampleTwitter.API.Models;

namespace SampleTwitter.API.IntegrationTests.Users;

public class GetUserTests : IntegrationTestBase
{
    public GetUserTests(ApiWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task InvalidIdFormat_Returns404()
    {
        // Act
        var message = new HttpRequestMessage(HttpMethod.Get, "/api/users/not-a-valid-id");
        var response = await Client.SendAsync(message);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_Returns200()
    {
        // Arrange
        var user = await SeedUser("user@example.com", "Sup3rSecret1!");

        // Act (no auth cookie)
        var response = await Client.GetAsync($"/api/users/{user.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedViewer_Returns200()
    {
        // Arrange
        var (_, cookie) = await SeedAndSignIn("viewer@example.com", "Sup3rSecret1!");
        var author = await SeedUser("author@example.com", "Sup3rSecret1!");

        var message = new HttpRequestMessage(HttpMethod.Get, $"/api/users/{author.Id}")
        {
            Headers = { { "Cookie", cookie } }
        };

        // Act
        var response = await Client.SendAsync(message);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task NonExistentUserId_Returns404()
    {
        // Arrange
        var (_, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");
                const long nonExistentUserId = 999999;

        // Act
        var message = new HttpRequestMessage(HttpMethod.Get, $"/api/users/{nonExistentUserId}")
        {
            Headers = { { "Cookie", cookie } }
        };
        var response = await Client.SendAsync(message);

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MapsAllFieldsCorrectly()
    {
        // Arrange
        var user = await SeedUser("alice@example.com", "Sup3rSecret1!");

        // Act
        var response = await Client.GetAsync($"/api/users/{user.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(body);

        Assert.Equal(user.Id, body.Id);
        Assert.Equal(user.Email, body.Email);
        Assert.Equal(user.RegisteredAt.ToUnixTimeMilliseconds(), body.RegisteredAt.ToUnixTimeMilliseconds());
    }

    [Fact]
    public async Task ZeroUserId_Returns404()
    {
        // Act
        var response = await Client.GetAsync("/api/users/0");

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task NegativeUserId_Returns404()
    {
        // Act
        var response = await Client.GetAsync("/api/users/-1");

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DecimalUserId_Returns404()
    {
        // Act
        var response = await Client.GetAsync("/api/users/1.5");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task OverflowUserId_Returns404()
    {
        // Act
        var response = await Client.GetAsync("/api/users/99999999999999999999999999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task MaxLongUserId_NonExistent_Returns404()
    {
        // Act
        var response = await Client.GetAsync($"/api/users/{long.MaxValue}");

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ResponseDoesNotContainSensitiveFields()
    {
        // Arrange
        var user = await SeedUser("user_sensitive@example.com", "Sup3rSecret1!");

        // Act
        var response = await Client.GetAsync($"/api/users/{user.Id}");
        var rawJson = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("passwordHash", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", rawJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unauthenticated_NonExistentUserId_Returns404WithProblemDetails()
    {
        // Act (no auth cookie)
        var response = await Client.GetAsync("/api/users/999999");

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AuthenticatedSelfViewer_Returns200()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("self_viewer@example.com", "Sup3rSecret1!");

        var message = new HttpRequestMessage(HttpMethod.Get, $"/api/users/{user.Id}")
        {
            Headers = { { "Cookie", cookie } }
        };

        // Act
        var response = await Client.SendAsync(message);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(body);
        Assert.Equal(user.Id, body.Id);
        Assert.Equal(user.Email, body.Email);
    }

    [Fact]
    public async Task InvalidOrTamperedCookie_Returns200()
    {
        // Arrange
        var user = await SeedUser("public_tampered@example.com", "Sup3rSecret1!");
        var message = new HttpRequestMessage(HttpMethod.Get, $"/api/users/{user.Id}")
        {
            Headers = { { "Cookie", "SampleTwitter.Auth=invalid_or_tampered_cookie_payload" } }
        };

        // Act
        var response = await Client.SendAsync(message);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(body);
        Assert.Equal(user.Id, body.Id);
        Assert.Equal(user.Email, body.Email);
    }

    [Fact]
    public async Task DeletedUser_Returns404WithProblemDetails()
    {
        // Arrange
        var user = await SeedUser("to_delete@example.com", "Sup3rSecret1!");
        var userId = user.Id;

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
            var dbUser = db.Users.Single(u => u.Id == userId);
            db.Users.Remove(dbUser);
            await db.SaveChangesAsync();
        }

        // Act
        var response = await Client.GetAsync($"/api/users/{userId}");

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MultipleUsersInDatabase_ReturnsRequestedUserOnly()
    {
        // Arrange
        await SeedUser("user1@example.com", "Sup3rSecret1!");
        var user2 = await SeedUser("user2@example.com", "Sup3rSecret1!");
        await SeedUser("user3@example.com", "Sup3rSecret1!");

        // Act
        var response = await Client.GetAsync($"/api/users/{user2.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(body);
        Assert.Equal(user2.Id, body.Id);
        Assert.Equal(user2.Email, body.Email);
    }

    [Fact]
    public async Task UnconfirmedEmailUser_Returns200()
    {
        // Arrange
        var user = await SeedUser("unconfirmed@example.com", "Sup3rSecret1!", emailConfirmed: false);

        // Act
        var response = await Client.GetAsync($"/api/users/{user.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(body);
        Assert.Equal(user.Id, body.Id);
        Assert.Equal(user.Email, body.Email);
    }

    [Fact]
    public async Task ComplexEmailFormatting_PreservedAccurately()
    {
        // Arrange
        const string email = "user.name+tag@sub.example.com";
        var user = await SeedUser(email, "Sup3rSecret1!");

        // Act
        var response = await Client.GetAsync($"/api/users/{user.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(body);
        Assert.Equal(email, body.Email);
    }

    private async Task<User> SeedUser(string email, string password, bool emailConfirmed = true)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var user = new User
        {
            Email = email,
            PasswordHash = hasher.Hash(password),
            EmailConfirmed = emailConfirmed,
            RegisteredAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private async Task<(User User, string Cookie)> SeedAndSignIn(string email, string password)
    {
        var user = await SeedUser(email, password);

        var loginResponse = await Client.PostAsJsonAsync("/api/auth/signin",
            new LoginRequest { Email = email, Password = password });

        loginResponse.EnsureSuccessStatusCode();

        var setCookieHeader = loginResponse.Headers.GetValues("Set-Cookie")
            .First(v => v.StartsWith("SampleTwitter.Auth="));
        var cookie = setCookieHeader.Split(';')[0];

        return (user, cookie);
    }
}