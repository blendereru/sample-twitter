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

    [Fact]
    public async Task EmailNotProvided_ReturnsBadRequestWithValidationProblemDetails()
    {
        // Arrange
        var request = new { Password = "Sup3rSecret1!" };

        // Act
        var response = await Client.PostAsJsonAsync("/api/account/signup", request);

        // Assert
        await response.AssertValidationProblemDetails("Email");
    }

    [Fact]
    public async Task PasswordNotProvided_ReturnsBadRequestWithValidationProblemDetails()
    {
        // Arrange
        var request = new { Email = "user@example.com" };
        
        // Act
        var response = await Client.PostAsJsonAsync("/api/account/signup", request);
        
        // Assert
        await response.AssertValidationProblemDetails("Password");
    }
    
    [Fact]
    public async Task WithPasswordShorterThanMinimumLength_ReturnsBadRequestWithValidationProblemDetails()
    {
        // Arrange
        var request = new SignUpRequest { Email = "new@example.com", Password = "short" };

        // Act
        var response = await Client.PostAsJsonAsync("/api/account/signup", request);

        // Assert
        await response.AssertValidationProblemDetails("Password");
    }

    [Fact]
    public async Task AlreadyConfirmedEmail_Returns409WithProblemDetails()
    {
        // Arrange
        using (var scope = Factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
            dbContext.Users.Add(new User
            {
                Email = "confirmed@example.com",
                PasswordHash = "existing-hash",
                EmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow.AddDays(-5)
            });
            await dbContext.SaveChangesAsync();
        }

        var request = new SignUpRequest { Email = "confirmed@example.com", Password = "AttemptedPassword1!" };
        
        // Act
        var response = await Client.PostAsJsonAsync("/api/account/signup", request);
        
        // Assert
        var problemDetails = await response.AssertProblemDetails(HttpStatusCode.Conflict);
        Assert.DoesNotContain("confirmed@example.com", problemDetails.Detail);
    }
    
    [Fact]
    public async Task AlreadyConfirmedEmail_DoesNotSendConfirmationEmail()
    {
        // Arrange
        using (var scope = Factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
            dbContext.Users.Add(new User
            {
                Email = "confirmed@example.com",
                PasswordHash = "existing-hash",
                EmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow.AddDays(-5)
            });
            await dbContext.SaveChangesAsync();
        }

        var request = new SignUpRequest { Email = "confirmed@example.com", Password = "AttemptedPassword1!" };

        // Act
        await Client.PostAsJsonAsync("/api/account/signup", request);

        // Assert
        Assert.Empty(Factory.FakeEmailSender.SentEmails);
    }
    
    [Fact]
    public async Task AlreadyConfirmedEmail_DoesNotModifyExistingUser()
    {
        // Arrange
        using (var scope = Factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
            dbContext.Users.Add(new User
            {
                Email = "confirmed@example.com",
                PasswordHash = "existing-hash",
                EmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow.AddDays(-5)
            });
            await dbContext.SaveChangesAsync();
        }

        var request = new SignUpRequest { Email = "confirmed@example.com", Password = "AttemptedPassword1!" };

        // Act
        await Client.PostAsJsonAsync("/api/account/signup", request);

        // Assert
        using var scope2 = Factory.Services.CreateScope();
        var dbContext2 = scope2.ServiceProvider.GetRequiredService<ApplicationContext>();
        var untouchedUser = await dbContext2.Users.AsNoTracking().SingleAsync(u => u.Email == "confirmed@example.com");
        Assert.Equal("existing-hash", untouchedUser.PasswordHash);
    }
    
    [Fact]
    public async Task UnconfirmedExistingEmail_Returns200NotCreated()
    {
        // Arrange
        using (var scope = Factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
            dbContext.Users.Add(new User
            {
                Email = "pending@example.com",
                PasswordHash = "old-hash",
                EmailConfirmed = false,
                RegisteredAt = DateTimeOffset.UtcNow.AddDays(-1)
            });
            await dbContext.SaveChangesAsync();
        }

        var request = new SignUpRequest { Email = "pending@example.com", Password = "NewPassword1!" };

        // Act
        var response = await Client.PostAsJsonAsync("/api/account/signup", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
    
    [Fact]
    public async Task UnconfirmedExistingEmail_UpdatesPasswordHash()
    {
        // Arrange
        using (var scope = Factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
            dbContext.Users.Add(new User
            {
                Email = "pending@example.com",
                PasswordHash = "old-hash",
                EmailConfirmed = false,
                RegisteredAt = DateTimeOffset.UtcNow.AddDays(-1)
            });
            await dbContext.SaveChangesAsync();
        }

        var request = new SignUpRequest { Email = "pending@example.com", Password = "NewPassword1!" };

        // Act
        await Client.PostAsJsonAsync("/api/account/signup", request);

        // Assert
        using var scope2 = Factory.Services.CreateScope();
        var dbContext2 = scope2.ServiceProvider.GetRequiredService<ApplicationContext>();
        var updatedUser = await dbContext2.Users.AsNoTracking().SingleAsync(u => u.Email == "pending@example.com");
        Assert.NotEqual("old-hash", updatedUser.PasswordHash);
    }
    
    [Fact]
    public async Task UnconfirmedExistingEmail_DoesNotCreateASecondUserRow()
    {
        // Arrange
        using (var scope = Factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
            dbContext.Users.Add(new User
            {
                Email = "pending@example.com",
                PasswordHash = "old-hash",
                EmailConfirmed = false,
                RegisteredAt = DateTimeOffset.UtcNow.AddDays(-1)
            });
            await dbContext.SaveChangesAsync();
        }

        var request = new SignUpRequest { Email = "pending@example.com", Password = "NewPassword1!" };

        // Act
        await Client.PostAsJsonAsync("/api/account/signup", request);

        // Assert
        using var scope2 = Factory.Services.CreateScope();
        var dbContext2 = scope2.ServiceProvider.GetRequiredService<ApplicationContext>();
        Assert.Equal(1, await dbContext2.Users.CountAsync(u => u.Email == "pending@example.com"));
    }
    
    [Fact]
    public async Task UnconfirmedExistingEmail_ResendsConfirmationEmail()
    {
        // Arrange
        using (var scope = Factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
            dbContext.Users.Add(new User
            {
                Email = "pending@example.com",
                PasswordHash = "old-hash",
                EmailConfirmed = false,
                RegisteredAt = DateTimeOffset.UtcNow.AddDays(-1)
            });
            await dbContext.SaveChangesAsync();
        }

        var request = new SignUpRequest { Email = "pending@example.com", Password = "NewPassword1!" };

        // Act
        await Client.PostAsJsonAsync("/api/account/signup", request);

        // Assert
        var sentEmail = Assert.Single(Factory.FakeEmailSender.SentEmails);
        Assert.Equal("pending@example.com", sentEmail.ToEmail);
    }
    
    [Fact]
    public async Task ConcurrentDuplicateRequests_OnlyOneSucceeds()
    {
        // Arrange
        var request = new SignUpRequest { Email = "race@example.com", Password = "Sup3rSecret1!" };

        // Act
        var task1 = Client.PostAsJsonAsync("/api/account/signup", request);
        var task2 = Client.PostAsJsonAsync("/api/account/signup", request);
        var responses = await Task.WhenAll(task1, task2);

        // Assert
        var statusCodes = responses.Select(r => r.StatusCode).OrderBy(s => s).ToList();
        Assert.Contains(HttpStatusCode.Created, statusCodes);
        Assert.Contains(HttpStatusCode.Conflict, statusCodes);
    }
    
    [Fact]
    public async Task ConcurrentDuplicateRequests_OnlyOneUserRowIsCreated()
    {
        // Arrange
        var request = new SignUpRequest { Email = "race@example.com", Password = "Sup3rSecret1!" };

        // Act
        var task1 = Client.PostAsJsonAsync("/api/account/signup", request);
        var task2 = Client.PostAsJsonAsync("/api/account/signup", request);
        await Task.WhenAll(task1, task2);

        // Assert
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        Assert.Equal(1, await dbContext.Users.CountAsync(u => u.Email == "race@example.com"));
    }
    
    [Fact]
    public async Task ConcurrentDuplicateRequests_TheConflictResponseHasWellFormedProblemDetails()
    {
        // Arrange
        var request = new SignUpRequest { Email = "race@example.com", Password = "Sup3rSecret1!" };

        // Act
        var task1 = Client.PostAsJsonAsync("/api/account/signup", request);
        var task2 = Client.PostAsJsonAsync("/api/account/signup", request);
        var responses = await Task.WhenAll(task1, task2);

        // Assert
        var conflictResponse = responses.Single(r => r.StatusCode == HttpStatusCode.Conflict);
        await conflictResponse.AssertProblemDetails(HttpStatusCode.Conflict);
    }
    
    [Fact]
    public async Task ConcurrentDuplicateRequests_OnlyOneConfirmationEmailIsSent()
    {
        // Arrange
        var request = new SignUpRequest { Email = "race@example.com", Password = "Sup3rSecret1!" };

        // Act
        var task1 = Client.PostAsJsonAsync("/api/account/signup", request);
        var task2 = Client.PostAsJsonAsync("/api/account/signup", request);
        await Task.WhenAll(task1, task2);

        // Assert
        var sentEmail = Assert.Single(Factory.FakeEmailSender.SentEmails);
        Assert.Equal("race@example.com", sentEmail.ToEmail);
    }
    
    [Fact]
    public async Task NewEmail_Returns201Created()
    {
        // Arrange
        var request = new SignUpRequest { Email = "new@example.com", Password = "Sup3rSecret1!" };

        // Act
        var response = await Client.PostAsJsonAsync("/api/account/signup", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
    
    [Fact]
    public async Task NewEmail_ReturnsLocationHeader()
    {
        // Arrange
        var request = new SignUpRequest { Email = "new@example.com", Password = "Sup3rSecret1!" };

        // Act
        var response = await Client.PostAsJsonAsync("/api/account/signup", request);

        // Assert
        Assert.NotNull(response.Headers.Location);
    }
    
    [Fact]
    public async Task NewEmail_ReturnsSignUpResponseWithUserId()
    {
        // Arrange
        var request = new SignUpRequest { Email = "new@example.com", Password = "Sup3rSecret1!" };

        // Act
        var response = await Client.PostAsJsonAsync("/api/account/signup", request);

        // Assert
        var body = await response.Content.ReadFromJsonAsync<SignUpResponse>();
        Assert.NotNull(body);
        Assert.True(body.UserId > 0);
    }

    [Fact]
    public async Task NewEmail_PersistsUnconfirmedUserInDatabase()
    {
        // Arrange
        var request = new SignUpRequest { Email = "new@example.com", Password = "Sup3rSecret1!" };

        // Act
        await Client.PostAsJsonAsync("/api/account/signup", request);

        // Assert
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var user = await dbContext.Users.AsNoTracking().SingleAsync(u => u.Email == "new@example.com");
        Assert.False(user.EmailConfirmed);
    }

    [Fact]
    public async Task NewEmail_DoesNotPersistThePasswordInPlainText()
    {
        // Arrange
        var request = new SignUpRequest { Email = "new@example.com", Password = "Sup3rSecret1!" };

        // Act
        await Client.PostAsJsonAsync("/api/account/signup", request);

        // Assert
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var user = await dbContext.Users.AsNoTracking().SingleAsync(u => u.Email == "new@example.com");
        Assert.NotEqual("Sup3rSecret1!", user.PasswordHash);
    }
    
    [Fact]
    public async Task NewEmail_SendsAConfirmationEmail()
    {
        // Arrange
        var request = new SignUpRequest { Email = "new@example.com", Password = "Sup3rSecret1!" };

        // Act
        await Client.PostAsJsonAsync("/api/account/signup", request);

        // Assert
        var sentEmail = Assert.Single(Factory.FakeEmailSender.SentEmails);
        Assert.Equal("new@example.com", sentEmail.ToEmail);
    }
    
    [Fact]
    public async Task NewEmail_WithUppercaseAndWhitespace_NormalizesBeforeStoring()
    {
        // Arrange
        var request = new SignUpRequest { Email = "  New@Example.com  ", Password = "Sup3rSecret1!" };

        // Act
        await Client.PostAsJsonAsync("/api/account/signup", request);

        // Assert
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var user = await dbContext.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Email == "new@example.com");
        Assert.NotNull(user);
    }
}