using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SampleTwitter.API.Data;
using SampleTwitter.API.DTOs.RequestDTOs;
using SampleTwitter.API.DTOs.ResponseDTOs;
using SampleTwitter.API.IntegrationTests.Infrastructure;

namespace SampleTwitter.API.IntegrationTests.Account;

public class ChangeEmailTests : IntegrationTestBase
{
    public ChangeEmailTests(ApiWebApplicationFactory factory) : base(factory) { }

    public static TheoryData<object, string> InvalidRequestCases => new()
    {
        { new { CurrentPassword = "Sup3rSecret1!" },                                    "NewEmail"        },
        { new { NewEmail = "", CurrentPassword = "Sup3rSecret1!" },                     "NewEmail"        },
        { new { NewEmail = "   ", CurrentPassword = "Sup3rSecret1!" },                  "NewEmail"        },
        { new { NewEmail = "not-an-email", CurrentPassword = "Sup3rSecret1!" },         "NewEmail"        },
        { new { NewEmail = "new@example.com" },                                         "CurrentPassword" },
        { new { NewEmail = "new@example.com", CurrentPassword = "" },                   "CurrentPassword" },
        { new { NewEmail = "new@example.com", CurrentPassword = "   " },                "CurrentPassword" }
    };

    [Theory]
    [MemberData(nameof(InvalidRequestCases))]
    public async Task InvalidRequest_Returns400WithValidationProblemDetailsNamingOffendingField(
        object requestBody, string expectedInvalidField)
    {
        // Arrange
        var (_, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");

        // Act
        var message = new HttpRequestMessage(HttpMethod.Put, "/api/auth/email")
        {
            Content = JsonContent.Create(requestBody),
            Headers = { { "Cookie", cookie } }
        };
        var response = await Client.SendAsync(message);

        // Assert
        await response.AssertValidationProblemDetails(expectedInvalidField);
        Assert.Empty(Factory.FakeEmailSender.SentEmails);
    }

    [Fact]
    public async Task ValidChangeEmail_Returns200WithChangeEmailResponse()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("old@example.com", "Sup3rSecret1!");

        // Act
        var response = await SendChangeEmailRequest(cookie,
            new ChangeEmailRequest { NewEmail = "new@example.com", CurrentPassword = "Sup3rSecret1!" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ChangeEmailResponse>();
        Assert.NotNull(body);
        Assert.Equal(user.Id, body.UserId);
        Assert.Equal("new@example.com", body.Email);
        Assert.False(string.IsNullOrWhiteSpace(body.Message));
    }

    [Fact]
    public async Task ValidChangeEmail_SignsOutSessionAndSendsConfirmationEmailAndPersistsToken()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("old@example.com", "Sup3rSecret1!");

        // Act
        var response = await SendChangeEmailRequest(cookie,
            new ChangeEmailRequest { NewEmail = "confirm_me@example.com", CurrentPassword = "Sup3rSecret1!" });

        // Assert — 200 OK and Set-Cookie header clears the auth cookie
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var setCookies));
        Assert.Contains(setCookies, c => c.StartsWith("SampleTwitter.Auth=;"));

        // Assert — confirmation email sent to the new address
        var sentEmail = Assert.Single(Factory.FakeEmailSender.SentEmails);
        Assert.Equal("confirm_me@example.com", sentEmail.ToEmail);
        Assert.Contains($"/confirm-email?userId={user.Id}&token=", sentEmail.HtmlBody);

        // Assert — confirmation token persisted in DB
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var token = await db.EmailConfirmationTokens.AsNoTracking().SingleOrDefaultAsync(t => t.UserId == user.Id);
        Assert.NotNull(token);
        Assert.Null(token.UsedAt);
        Assert.True(token.ExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task ValidChangeEmail_RequiresConfirmationBeforeLoginAndSucceedsAfterConfirmingEndToEnd()
    {
        // Arrange
        var (_, cookie) = await SeedAndSignIn("initial@example.com", "Sup3rSecret1!");

        // Act 1 — change email
        var changeResponse = await SendChangeEmailRequest(cookie,
            new ChangeEmailRequest { NewEmail = "verified_new@example.com", CurrentPassword = "Sup3rSecret1!" });
        Assert.Equal(HttpStatusCode.OK, changeResponse.StatusCode);

        // Assert 1 — login with old email fails with 401
        var oldLogin = await Client.PostAsJsonAsync("/api/auth/signin",
            new LoginRequest { Email = "initial@example.com", Password = "Sup3rSecret1!" });
        await oldLogin.AssertProblemDetails(HttpStatusCode.Unauthorized);

        // Assert 2 — login with new email before confirming fails with 403 (email not confirmed)
        var unconfirmedLogin = await Client.PostAsJsonAsync("/api/auth/signin",
            new LoginRequest { Email = "verified_new@example.com", Password = "Sup3rSecret1!" });
        await unconfirmedLogin.AssertProblemDetails(HttpStatusCode.Forbidden);

        // Act 2 — confirm the new email using the link sent to the new inbox
        var sentEmail = Assert.Single(Factory.FakeEmailSender.SentEmails);
        var confirmUrl = ExtractConfirmationUrl(sentEmail.HtmlBody);
        var confirmResponse = await Client.PostAsync(confirmUrl, content: null);
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);

        // Assert 3 — login with new email now succeeds with 200 OK
        var confirmedLogin = await Client.PostAsJsonAsync("/api/auth/signin",
            new LoginRequest { Email = "verified_new@example.com", Password = "Sup3rSecret1!" });
        Assert.Equal(HttpStatusCode.OK, confirmedLogin.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        // Act
        var response = await Client.PutAsJsonAsync("/api/auth/email",
            new ChangeEmailRequest { NewEmail = "new@example.com", CurrentPassword = "Sup3rSecret1!" });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task InvalidOrTamperedCookie_Returns401()
    {
        // Act
        var response = await SendChangeEmailRequest(
            "SampleTwitter.Auth=invalid_or_tampered_cookie",
            new ChangeEmailRequest { NewEmail = "new@example.com", CurrentPassword = "Sup3rSecret1!" });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WrongCurrentPassword_Returns401AndDoesNotMutateEmailOrSendConfirmation()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("secure@example.com", "Sup3rSecret1!");

        // Act
        var response = await SendChangeEmailRequest(cookie,
            new ChangeEmailRequest { NewEmail = "attacker@example.com", CurrentPassword = "WrongPassword1!" });

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.Unauthorized);
        Assert.Empty(Factory.FakeEmailSender.SentEmails);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var unchanged = await db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);

        Assert.Equal("secure@example.com", unchanged.Email);
        Assert.True(unchanged.EmailConfirmed);
        Assert.Null(unchanged.UpdatedAt);
    }

    [Fact]
    public async Task WrongCurrentPassword_WithAlreadyTakenNewEmail_Returns401Not409_PreventsEnumeration()
    {
        // Arrange
        await SeedUser("taken@example.com", "Sup3rSecret1!", emailConfirmed: true);
        var (_, cookie) = await SeedAndSignIn("caller@example.com", "Sup3rSecret1!");

        // Act — wrong password AND an email that already belongs to another user
        var response = await SendChangeEmailRequest(cookie,
            new ChangeEmailRequest { NewEmail = "taken@example.com", CurrentPassword = "WrongPassword1!" });

        // Assert — must return 401 (password checked first), not 409
        await response.AssertProblemDetails(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeletedUserWithValidCookie_Returns404WithProblemDetails()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("deleted@example.com", "Sup3rSecret1!");

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
            var dbUser = await db.Users.SingleAsync(u => u.Id == user.Id);
            db.Users.Remove(dbUser);
            await db.SaveChangesAsync();
        }

        // Act
        var response = await SendChangeEmailRequest(cookie,
            new ChangeEmailRequest { NewEmail = "new@example.com", CurrentPassword = "Sup3rSecret1!" });

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task NewEmailAlreadyUsedByAnotherConfirmedUser_Returns409AndDoesNotMutateOrSendEmail()
    {
        // Arrange
        await SeedUser("existing@example.com", "Sup3rSecret1!", emailConfirmed: true);
        var (caller, cookie) = await SeedAndSignIn("caller@example.com", "Sup3rSecret1!");

        // Act
        var response = await SendChangeEmailRequest(cookie,
            new ChangeEmailRequest { NewEmail = "existing@example.com", CurrentPassword = "Sup3rSecret1!" });

        // Assert
        var problemDetails = await response.AssertProblemDetails(HttpStatusCode.Conflict);
        Assert.DoesNotContain("existing@example.com", problemDetails.Detail ?? string.Empty);
        Assert.Empty(Factory.FakeEmailSender.SentEmails);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var unchanged = await db.Users.AsNoTracking().SingleAsync(u => u.Id == caller.Id);
        Assert.Equal("caller@example.com", unchanged.Email);
        Assert.True(unchanged.EmailConfirmed);
    }

    [Fact]
    public async Task NewEmailAlreadyUsedByAnotherUnconfirmedUser_Returns409AndDoesNotMutateOrSendEmail()
    {
        // Arrange
        await SeedUser("pending@example.com", "Sup3rSecret1!", emailConfirmed: false);
        var (caller, cookie) = await SeedAndSignIn("caller@example.com", "Sup3rSecret1!");

        // Act
        var response = await SendChangeEmailRequest(cookie,
            new ChangeEmailRequest { NewEmail = "pending@example.com", CurrentPassword = "Sup3rSecret1!" });

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.Conflict);
        Assert.Empty(Factory.FakeEmailSender.SentEmails);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var unchanged = await db.Users.AsNoTracking().SingleAsync(u => u.Id == caller.Id);
        Assert.Equal("caller@example.com", unchanged.Email);
        Assert.True(unchanged.EmailConfirmed);
    }

    [Fact]
    public async Task NewEmailSameAsCurrentEmail_Returns409AndDoesNotMutateOrSendEmail()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("same@example.com", "Sup3rSecret1!");

        // Act
        var response = await SendChangeEmailRequest(cookie,
            new ChangeEmailRequest { NewEmail = "  SAME@Example.com  ", CurrentPassword = "Sup3rSecret1!" });

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.Conflict);
        Assert.Empty(Factory.FakeEmailSender.SentEmails);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var unchanged = await db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        Assert.Equal("same@example.com", unchanged.Email);
        Assert.True(unchanged.EmailConfirmed);
    }

    [Fact]
    public async Task ConcurrentChangeEmail_ToSameNewEmail_HandlesPostgresUniqueConstraintRaceCondition()
    {
        // Arrange
        var (_, cookie1) = await SeedAndSignIn("user1@example.com", "Sup3rSecret1!");
        var (_, cookie2) = await SeedAndSignIn("user2@example.com", "Sup3rSecret1!");

        const string targetEmail = "race_target@example.com";
        var request = new ChangeEmailRequest { NewEmail = targetEmail, CurrentPassword = "Sup3rSecret1!" };

        using var barrier = Factory.UserInsertBarrier.Arm(participants: 2, timeout: TimeSpan.FromSeconds(10));

        // Act — two authenticated users concurrently update their email to the exact same address
        var responses = await Task.WhenAll(
            SendChangeEmailRequest(cookie1, request),
            SendChangeEmailRequest(cookie2, request));

        // Assert — both passed the AnyAsync check and reached SaveChanges simultaneously
        Assert.Equal(2, barrier.ArrivedCount);

        // Assert — one winner (200 OK) and one loser (409 Conflict)
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        var loser = Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        await loser.AssertProblemDetails(HttpStatusCode.Conflict);

        // Assert — only 1 confirmation email was sent
        var sentEmail = Assert.Single(Factory.FakeEmailSender.SentEmails);
        Assert.Equal(targetEmail, sentEmail.ToEmail);
    }

    private Task<HttpResponseMessage> SendChangeEmailRequest(string cookie, ChangeEmailRequest request)
    {
        var message = new HttpRequestMessage(HttpMethod.Put, "/api/auth/email")
        {
            Content = JsonContent.Create(request),
            Headers = { { "Cookie", cookie } }
        };
        return Client.SendAsync(message);
    }

    private static string ExtractConfirmationUrl(string htmlBody)
    {
        var hrefStart = htmlBody.IndexOf("href=\"", StringComparison.Ordinal) + 6;
        var hrefEnd = htmlBody.IndexOf("\"", hrefStart, StringComparison.Ordinal);
        var fullUrl = htmlBody[hrefStart..hrefEnd];

        var uri = new Uri(fullUrl.Replace("&amp;", "&"));
        return $"/api/auth/confirm-email{uri.Query}";
    }
}