using System.Net;
using System.Net.Http.Json;
using SampleTwitter.API.DTOs.ResponseDTOs;
using SampleTwitter.API.IntegrationTests.Infrastructure;

namespace SampleTwitter.API.IntegrationTests.Account;

public class SignOutTests : IntegrationTestBase
{
    public SignOutTests(ApiWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task AuthenticatedUser_Returns200AndClearsAuthCookie()
    {
        var (_, cookie) = await SeedAndSignIn("signout_user@example.com", "Sup3rSecret1!");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/signout")
        {
            Headers = { { "Cookie", cookie } }
        };

        var response = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<SignOutResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.Message));

        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var setCookies));
        Assert.Contains(setCookies, c => c.StartsWith("SampleTwitter.Auth=;"));
    }

    [Fact]
    public async Task UnauthenticatedUser_Returns401Unauthorized()
    {
        var response = await Client.PostAsync("/api/auth/signout", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task InvalidOrTamperedCookie_Returns401Unauthorized()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/signout")
        {
            Headers = { { "Cookie", "SampleTwitter.Auth=invalid_cookie_value" } }
        };

        var response = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}