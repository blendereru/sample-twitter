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

public class ResendConfirmationEmailTests : IntegrationTestBase
{
    public ResendConfirmationEmailTests(ApiWebApplicationFactory factory) : base(factory) { }

    public static TheoryData<object, string> InvalidRequestCases => new()
    {
        { new { },                                                              "Email" },
        { new { Email = "" },                                                   "Email" },
        { new { Email = "   " },                                                "Email" },
        { new { Email = "not-an-email" },                                       "Email" },
        { new ResendConfirmationEmailRequest { Email = "missing-at-sign.com" }, "Email" }
    };

    [Theory]
    [MemberData(nameof(InvalidRequestCases))]
    public async Task InvalidRequest_Returns400WithValidationProblemDetailsNamingEmail(
        object requestBody, string expectedInvalidField)
    {
        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/resend-confirmation-email", requestBody);

        // Assert
        await response.AssertValidationProblemDetails(expectedInvalidField);
        Assert.Empty(Factory.FakeEmailSender.SentEmails);
    }

    [Fact]
    public async Task UnconfirmedUser_Returns200AndInvalidatesOldTokensAndSendsNewConfirmationEmail()
    {
        // Arrange
        var user = await SeedUser("unconfirmed@example.com", "Sup3rSecret1!", emailConfirmed: false);

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
            db.EmailConfirmationTokens.Add(EmailConfirmationToken.Create(user.Id, "old-token-hash"));
            await db.SaveChangesAsync();
        }

        // Act
        var response = await Client.PostAsJsonAsync(
            "/api/auth/resend-confirmation-email",
            new ResendConfirmationEmailRequest { Email = "  UNCONFIRMED@example.com  " });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ResendConfirmationEmailResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.Message));

        var sentEmail = Assert.Single(Factory.FakeEmailSender.SentEmails);
        Assert.Equal("unconfirmed@example.com", sentEmail.ToEmail);
        Assert.Contains($"/confirm-email?userId={user.Id}&token=", sentEmail.HtmlBody);

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
            var tokens = await db.EmailConfirmationTokens
                .AsNoTracking()
                .Where(t => t.UserId == user.Id)
                .ToListAsync();

            var activeToken = Assert.Single(tokens);
            Assert.NotEqual("old-token-hash", activeToken.TokenHash);
            Assert.Null(activeToken.UsedAt);
        }

        var confirmUrl = ExtractConfirmationUrl(sentEmail.HtmlBody);
        var confirmResponse = await Client.PostAsync(confirmUrl, content: null);
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
    }

    [Fact]
    public async Task ConfirmedUser_Returns200AndDoesNotSendEmailOrCreateToken()
    {
        // Arrange
        var user = await SeedUser("confirmed@example.com", "Sup3rSecret1!", emailConfirmed: true);

        // Act
        var response = await Client.PostAsJsonAsync(
            "/api/auth/resend-confirmation-email",
            new ResendConfirmationEmailRequest { Email = "confirmed@example.com" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(Factory.FakeEmailSender.SentEmails);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        Assert.False(await db.EmailConfirmationTokens.AnyAsync(t => t.UserId == user.Id));
    }

    [Fact]
    public async Task NonExistentEmail_Returns200AndDoesNotSendEmail()
    {
        // Act
        var response = await Client.PostAsJsonAsync(
            "/api/auth/resend-confirmation-email",
            new ResendConfirmationEmailRequest { Email = "nobody@example.com" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(Factory.FakeEmailSender.SentEmails);
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