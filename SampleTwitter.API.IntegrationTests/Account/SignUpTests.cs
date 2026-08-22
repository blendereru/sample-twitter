using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SampleTwitter.API.Data;
using SampleTwitter.API.DTOs.RequestDTOs;
using SampleTwitter.API.DTOs.ResponseDTOs;
using SampleTwitter.API.IntegrationTests.Infrastructure;
using SampleTwitter.API.Models;

namespace SampleTwitter.API.IntegrationTests.Account;

public class SignUpTests : IntegrationTestBase
{
    public SignUpTests(ApiWebApplicationFactory factory) : base(factory) { }
    

    public static TheoryData<object, string> InvalidRequestCases => new()
    {
        { new { Password = "Sup3rSecret1!" },                                      "Email"    },
        { new { Email    = "user@example.com" },                                   "Password" },
        { new SignUpRequest { Email = "new@example.com", Password = "short" },     "Password" }
    };

    [Theory]
    [MemberData(nameof(InvalidRequestCases))]
    public async Task InvalidRequest_Returns400WithValidationProblemDetailsNamingTheOffendingField(
        object requestBody, string expectedInvalidField)
    {
        // Act
        var response = await Client.PostAsJsonAsync("/api/account/signup", requestBody);

        // Assert
        await response.AssertValidationProblemDetails(expectedInvalidField);
    }
    
    [Fact]
    public async Task AlreadyConfirmedEmail_Returns409AndDoesNotModifyStateOrSendEmail()
    {
        // Arrange
        await SeedConfirmedUser("confirmed@example.com", passwordHash: "existing-hash");

        var request = new SignUpRequest { Email = "confirmed@example.com", Password = "AttemptedPassword1!" };

        // Act
        var response = await Client.PostAsJsonAsync("/api/account/signup", request);

        // Assert — HTTP contract
        var problemDetails = await response.AssertProblemDetails(HttpStatusCode.Conflict);

        // Assert — email address is not leaked in the public error message
        Assert.DoesNotContain("confirmed@example.com", problemDetails.Detail ?? string.Empty);

        // Assert — no email was dispatched
        Assert.Empty(Factory.FakeEmailSender.SentEmails);

        // Assert — the existing user row was not mutated
        var untouched = await QueryUserAsync("confirmed@example.com");
        Assert.NotNull(untouched);
        Assert.Equal("existing-hash", untouched.PasswordHash);
        Assert.True(untouched.EmailConfirmed);
    }
    
    [Fact]
    public async Task UnconfirmedExistingEmail_Returns200AndUpdatesStateAndResendsEmail()
    {
        // Arrange
        await SeedUnconfirmedUser("pending@example.com", passwordHash: "old-hash");

        var request = new SignUpRequest { Email = "pending@example.com", Password = "NewPassword1!" };

        // Act
        var response = await Client.PostAsJsonAsync("/api/account/signup", request);

        // Assert — HTTP contract
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<SignUpResponse>();
        Assert.NotNull(body);
        Assert.True(body.UserId > 0);

        // Assert — exactly one confirmation email re-sent to the right address
        var sentEmail = Assert.Single(Factory.FakeEmailSender.SentEmails);
        Assert.Equal("pending@example.com", sentEmail.ToEmail);

        // Assert — DB: still exactly one row, password hash updated, not yet confirmed
        var users = await QueryAllUsersAsync("pending@example.com");
        var user = Assert.Single(users);
        Assert.NotEqual("old-hash", user.PasswordHash);
        Assert.False(user.EmailConfirmed);
    }
    
    [Fact]
    public async Task NewEmail_Returns201AndPersistsUserAndSendsConfirmationEmail()
    {
        // Arrange
        var request = new SignUpRequest { Email = "new@example.com", Password = "Sup3rSecret1!" };

        // Act
        var response = await Client.PostAsJsonAsync("/api/account/signup", request);

        // Assert — HTTP contract
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var body = await response.Content.ReadFromJsonAsync<SignUpResponse>();
        Assert.NotNull(body);
        Assert.True(body.UserId > 0);

        // Assert — confirmation email sent to the right address
        var sentEmail = Assert.Single(Factory.FakeEmailSender.SentEmails);
        Assert.Equal("new@example.com", sentEmail.ToEmail);

        // Assert — DB: user persisted, not yet confirmed, password hashed
        var user = await QueryUserAsync("new@example.com");
        Assert.NotNull(user);
        Assert.False(user.EmailConfirmed);
        Assert.NotEqual("Sup3rSecret1!", user.PasswordHash);
    }
    
    [Fact]
    public async Task NewEmail_WithLeadingTrailingWhitespaceAndMixedCase_IsNormalisedBeforeStoring()
    {
        // Arrange
        var request = new SignUpRequest { Email = "  New@Example.com  ", Password = "Sup3rSecret1!" };

        // Act
        var response = await Client.PostAsJsonAsync("/api/account/signup", request);

        // Assert — request succeeded
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // Assert — stored under the normalised form, not the original input
        var normalised = await QueryUserAsync("new@example.com");
        Assert.NotNull(normalised);

        var original = await QueryUserAsync("  New@Example.com  ");
        Assert.Null(original);
    }
    
    [Fact]
    public async Task SignUpForAlreadyConfirmedEmail_Returns409WithWellFormedProblemDetails()
    {
        // Arrange
        await SeedConfirmedUser("taken@example.com", passwordHash: "hash");

        var request = new SignUpRequest { Email = "taken@example.com", Password = "NewP@ss1!" };

        // Act
        var response = await Client.PostAsJsonAsync("/api/account/signup", request);

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.Conflict);
    }

    private async Task SeedConfirmedUser(string email, string passwordHash)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        db.Users.Add(new User
        {
            Email = email,
            PasswordHash = passwordHash,
            EmailConfirmed = true,
            RegisteredAt = DateTimeOffset.UtcNow.AddDays(-5)
        });
        await db.SaveChangesAsync();
    }

    private async Task SeedUnconfirmedUser(string email, string passwordHash)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        db.Users.Add(new User
        {
            Email = email,
            PasswordHash = passwordHash,
            EmailConfirmed = false,
            RegisteredAt = DateTimeOffset.UtcNow.AddDays(-1)
        });
        await db.SaveChangesAsync();
    }

    private async Task<User?> QueryUserAsync(string email)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        return await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Email == email);
    }

    private async Task<List<User>> QueryAllUsersAsync(string email)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        return await db.Users.AsNoTracking().Where(u => u.Email == email).ToListAsync();
    }
}