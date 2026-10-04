using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SampleTwitter.API.Abstractions;
using SampleTwitter.API.Data;
using SampleTwitter.API.DTOs.RequestDTOs;
using SampleTwitter.API.DTOs.ResponseDTOs;
using SampleTwitter.API.Models;

namespace SampleTwitter.API.IntegrationTests.Infrastructure;

[Collection("Integration")]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected readonly ApiWebApplicationFactory Factory;
    protected readonly HttpClient Client;

    protected IntegrationTestBase(ApiWebApplicationFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
            AllowAutoRedirect = false
        });
    }

    public async Task InitializeAsync()
    {
        await Factory.ResetDatabaseAsync();
        Factory.FakeEmailSender.Clear();
    }

    public Task DisposeAsync()
    {
        Client.Dispose();
        return Task.CompletedTask;
    }

    protected async Task<User> SeedUser(string email, string password, bool emailConfirmed = true)
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

    protected async Task<(User User, string Cookie)> SeedAndSignIn(string email, string password)
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

    protected async Task<Post> SeedPost(
        long userId,
        string? text = null,
        string? imageUrl = null,
        long? replyId = null,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? updatedAt = null)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

        var post = new Post
        {
            Text = text,
            ImageUrl = imageUrl,
            ReplyId = replyId,
            UserId = userId,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
            UpdatedAt = updatedAt
        };
        db.Posts.Add(post);
        await db.SaveChangesAsync();
        return post;
    }

    protected async Task<Post> SeedReply(long userId, long replyId, string text)
    {
        return await SeedPost(userId: userId, text: text, replyId: replyId);
    }

    protected async Task<Repost> SeedRepost(long userId, long postId, DateTimeOffset? createdAt = null)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

        var repost = new Repost
        {
            PostId = postId,
            UserId = userId,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow
        };
        db.Reposts.Add(repost);
        await db.SaveChangesAsync();
        return repost;
    }

    protected async Task<long> GetUserId(string cookie)
    {
        var message = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me")
        {
            Headers = { { "Cookie", cookie } }
        };
        var response = await Client.SendAsync(message);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<MeResponse>();
        return body!.Id;
    }
}