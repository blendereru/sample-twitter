using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SampleTwitter.API.Data;
using SampleTwitter.API.DTOs.RequestDTOs;
using SampleTwitter.API.DTOs.ResponseDTOs;
using SampleTwitter.API.IntegrationTests.Infrastructure;

namespace SampleTwitter.API.IntegrationTests.Users;

public class EditProfileTests : IntegrationTestBase
{
    public EditProfileTests(ApiWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task ValidEdit_UsernameOnly_Returns200WithEditProfileResponse()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");

        // Act
        var response = await SendEditProfileRequest(cookie, user.Id,
            new EditProfileRequest { Username = "alice_dev", ImageUrl = null });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<EditProfileResponse>();
        Assert.NotNull(body);
        Assert.Equal(user.Id, body.Id);
        Assert.Equal("alice_dev", body.Username);
        Assert.Null(body.ImageUrl);
        Assert.NotNull(body.UpdatedAt);
    }

    [Fact]
    public async Task ValidEdit_ImageOnly_Returns200WithEditProfileResponse()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");
        const string avatarUrl = "https://example.com/avatar.png";

        // Act
        var response = await SendEditProfileRequest(cookie, user.Id,
            new EditProfileRequest { Username = null, ImageUrl = avatarUrl });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<EditProfileResponse>();
        Assert.NotNull(body);
        Assert.Equal(user.Id, body.Id);
        Assert.Null(body.Username);
        Assert.Equal(avatarUrl, body.ImageUrl);
        Assert.NotNull(body.UpdatedAt);
    }

    [Fact]
    public async Task ValidEdit_BothUsernameAndImageUrl_Returns200WithEditProfileResponse()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");
        const string avatarUrl = "https://example.com/avatar.png";

        // Act
        var response = await SendEditProfileRequest(cookie, user.Id,
            new EditProfileRequest { Username = "Alice", ImageUrl = avatarUrl });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<EditProfileResponse>();
        Assert.NotNull(body);
        Assert.Equal(user.Id, body.Id);
        Assert.Equal("Alice", body.Username);
        Assert.Equal(avatarUrl, body.ImageUrl);
        Assert.NotNull(body.UpdatedAt);
    }

    [Fact]
    public async Task ValidEdit_PersistsChangesInDatabaseAndPreservesOtherFields()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");
        const string avatarUrl = "https://example.com/avatar.png";

        // Act
        var response = await SendEditProfileRequest(cookie, user.Id,
            new EditProfileRequest { Username = "PersistedUser", ImageUrl = avatarUrl });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var updated = db.Users.Single(u => u.Id == user.Id);

        Assert.Equal("PersistedUser", updated.Username);
        Assert.Equal(avatarUrl, updated.ImageUrl);
        Assert.NotNull(updated.UpdatedAt);
        Assert.Equal(user.Email, updated.Email);
        Assert.Equal(user.PasswordHash, updated.PasswordHash);
        Assert.True(updated.EmailConfirmed);
        Assert.Equal(user.RegisteredAt.ToUnixTimeMilliseconds(), updated.RegisteredAt.ToUnixTimeMilliseconds());
    }

    [Fact]
    public async Task ValidEdit_ReflectedInGetUserAndMeEndpoints()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");
        const string avatarUrl = "https://example.com/avatar.png";

        // Act
        var editResponse = await SendEditProfileRequest(cookie, user.Id,
            new EditProfileRequest { Username = "VisibleName", ImageUrl = avatarUrl });
        Assert.Equal(HttpStatusCode.OK, editResponse.StatusCode);

        var getUserResponse = await Client.GetAsync($"/api/users/{user.Id}");
        var meMessage = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me")
        {
            Headers = { { "Cookie", cookie } }
        };
        var meResponse = await Client.SendAsync(meMessage);

        // Assert
        Assert.Equal(HttpStatusCode.OK, getUserResponse.StatusCode);
        var userBody = await getUserResponse.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(userBody);
        Assert.Equal("VisibleName", userBody.Username);
        Assert.Equal(avatarUrl, userBody.ImageUrl);
        Assert.NotNull(userBody.UpdatedAt);

        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        var meBody = await meResponse.Content.ReadFromJsonAsync<MeResponse>();
        Assert.NotNull(meBody);
        Assert.Equal("VisibleName", meBody.Username);
        Assert.Equal(avatarUrl, meBody.ImageUrl);
        Assert.NotNull(meBody.UpdatedAt);
    }

    [Fact]
    public async Task ValidEdit_UsernameWithWhitespace_IsTrimmedAndPersisted()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");

        // Act
        var response = await SendEditProfileRequest(cookie, user.Id,
            new EditProfileRequest { Username = "   trimmed_name   " });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<EditProfileResponse>();
        Assert.NotNull(body);
        Assert.Equal("trimmed_name", body.Username);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var updated = db.Users.Single(u => u.Id == user.Id);
        Assert.Equal("trimmed_name", updated.Username);
    }

    [Fact]
    public async Task ValidEdit_WhitespaceUsernameWithValidImageUrl_PersistsNullUsername()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");
        const string avatarUrl = "https://example.com/avatar.png";

        // Act
        var response = await SendEditProfileRequest(cookie, user.Id,
            new EditProfileRequest { Username = "   ", ImageUrl = avatarUrl });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<EditProfileResponse>();
        Assert.NotNull(body);
        Assert.Null(body.Username);
        Assert.Equal(avatarUrl, body.ImageUrl);
    }

    [Fact]
    public async Task ValidEdit_RemoveExistingImageUrl_PersistsNullImageUrl()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");
        await SendEditProfileRequest(cookie, user.Id,
            new EditProfileRequest { Username = "Alice", ImageUrl = "https://example.com/old.png" });

        // Act
        var response = await SendEditProfileRequest(cookie, user.Id,
            new EditProfileRequest { Username = "Alice", ImageUrl = null });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var updated = db.Users.Single(u => u.Id == user.Id);

        Assert.Equal("Alice", updated.Username);
        Assert.Null(updated.ImageUrl);
    }

    [Fact]
    public async Task ValidEdit_RemoveExistingUsername_PersistsNullUsername()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");
        const string avatarUrl = "https://example.com/avatar.png";
        await SendEditProfileRequest(cookie, user.Id,
            new EditProfileRequest { Username = "Alice", ImageUrl = avatarUrl });

        // Act
        var response = await SendEditProfileRequest(cookie, user.Id,
            new EditProfileRequest { Username = null, ImageUrl = avatarUrl });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var updated = db.Users.Single(u => u.Id == user.Id);

        Assert.Null(updated.Username);
        Assert.Equal(avatarUrl, updated.ImageUrl);
    }

    [Fact]
    public async Task ValidEdit_DuplicateUsernameAcrossTwoUsers_Returns200ForBoth()
    {
        // Arrange
        var (user1, cookie1) = await SeedAndSignIn("user1@example.com", "Sup3rSecret1!");
        var (user2, cookie2) = await SeedAndSignIn("user2@example.com", "Sup3rSecret1!");

        // Act
        var response1 = await SendEditProfileRequest(cookie1, user1.Id,
            new EditProfileRequest { Username = "SameDisplayName" });
        var response2 = await SendEditProfileRequest(cookie2, user2.Id,
            new EditProfileRequest { Username = "SameDisplayName" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
    }

    [Fact]
    public async Task ValidEdit_Exactly50Characters_Returns200AndPersists()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");
        var maxUsername = new string('u', 50);

        // Act
        var response = await SendEditProfileRequest(cookie, user.Id,
            new EditProfileRequest { Username = maxUsername });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var updated = db.Users.Single(u => u.Id == user.Id);

        Assert.Equal(maxUsername, updated.Username);
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        // Arrange
        var user = await SeedUser("user@example.com", "Sup3rSecret1!");

        // Act
        var response = await Client.PutAsJsonAsync($"/api/users/{user.Id}",
            new EditProfileRequest { Username = "unauth_edit" });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task InvalidOrTamperedCookie_Returns401()
    {
        // Arrange
        var user = await SeedUser("user@example.com", "Sup3rSecret1!");

        // Act
        var response = await SendEditProfileRequest(
            "SampleTwitter.Auth=invalid_or_tampered_cookie",
            user.Id,
            new EditProfileRequest { Username = "tampered_edit" });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DifferentUser_Returns403()
    {
        // Arrange
        var (user1, _) = await SeedAndSignIn("user1@example.com", "Sup3rSecret1!");
        var (_, user2Cookie) = await SeedAndSignIn("user2@example.com", "Sup3rSecret1!");

        // Act
        var response = await SendEditProfileRequest(user2Cookie, user1.Id,
            new EditProfileRequest { Username = "hacked_by_user2" });

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DifferentUser_DoesNotMutateTargetUserProfile()
    {
        // Arrange
        var (user1, cookie1) = await SeedAndSignIn("user1@example.com", "Sup3rSecret1!");
        await SendEditProfileRequest(cookie1, user1.Id,
            new EditProfileRequest { Username = "OriginalName" });

        var (_, user2Cookie) = await SeedAndSignIn("user2@example.com", "Sup3rSecret1!");

        // Act
        await SendEditProfileRequest(user2Cookie, user1.Id,
            new EditProfileRequest { Username = "HackedName" });

        // Assert
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var unchanged = db.Users.Single(u => u.Id == user1.Id);

        Assert.Equal("OriginalName", unchanged.Username);
    }

    [Fact]
    public async Task NonExistentUserId_Returns404()
    {
        // Arrange
        var (_, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");

        // Act
        var response = await SendEditProfileRequest(cookie, userId: 999999,
            new EditProfileRequest { Username = "ghost_user" });

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task InvalidIdFormat_Returns404()
    {
        // Arrange
        var (_, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");

        // Act
        var message = new HttpRequestMessage(HttpMethod.Put, "/api/users/not-a-valid-id")
        {
            Content = JsonContent.Create(new EditProfileRequest { Username = "Alice" }),
            Headers = { { "Cookie", cookie } }
        };
        var response = await Client.SendAsync(message);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task EmptyBody_NullUsernameAndNullImageUrl_Returns400()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");

        // Act
        var response = await SendEditProfileRequest(cookie, user.Id,
            new EditProfileRequest { Username = null, ImageUrl = null });

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task EmptyBody_WhitespaceUsernameAndNullImageUrl_Returns400()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");

        // Act
        var response = await SendEditProfileRequest(cookie, user.Id,
            new EditProfileRequest { Username = "   ", ImageUrl = null });

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task FailedEdit_EmptyBody_DoesNotMutateExistingProfile()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");
        await SendEditProfileRequest(cookie, user.Id,
            new EditProfileRequest { Username = "InitialName" });

        // Act
        await SendEditProfileRequest(cookie, user.Id,
            new EditProfileRequest { Username = null, ImageUrl = null });

        // Assert
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var unchanged = db.Users.Single(u => u.Id == user.Id);

        Assert.Equal("InitialName", unchanged.Username);
    }

    [Fact]
    public async Task UsernameExceeds50Characters_Returns400WithUsernameValidationError()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");

        // Act
        var response = await SendEditProfileRequest(cookie, user.Id,
            new EditProfileRequest { Username = new string('x', 51) });

        // Assert
        await response.AssertValidationProblemDetails("Username");
    }

    [Fact]
    public async Task FailedEdit_UsernameExceeds50Characters_DoesNotMutateExistingProfile()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");
        await SendEditProfileRequest(cookie, user.Id,
            new EditProfileRequest { Username = "UntouchedName" });

        // Act
        await SendEditProfileRequest(cookie, user.Id,
            new EditProfileRequest { Username = new string('x', 51) });

        // Assert
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var unchanged = db.Users.Single(u => u.Id == user.Id);

        Assert.Equal("UntouchedName", unchanged.Username);
    }

    [Fact]
    public async Task InvalidImageUrl_Returns400WithImageUrlValidationError()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");

        // Act
        var response = await SendEditProfileRequest(cookie, user.Id,
            new EditProfileRequest { Username = "ValidUsername", ImageUrl = "not-a-valid-url" });

        // Assert
        await response.AssertValidationProblemDetails("ImageUrl");
    }

    private Task<HttpResponseMessage> SendEditProfileRequest(string cookie, long userId, EditProfileRequest request)
    {
        var message = new HttpRequestMessage(HttpMethod.Put, $"/api/users/{userId}")
        {
            Content = JsonContent.Create(request),
            Headers = { { "Cookie", cookie } }
        };
        return Client.SendAsync(message);
    }
}