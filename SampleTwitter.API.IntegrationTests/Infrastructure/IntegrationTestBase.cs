namespace SampleTwitter.API.IntegrationTests.Infrastructure;

[Collection("Integration")]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected readonly ApiWebApplicationFactory Factory;
    protected readonly HttpClient Client;

    protected IntegrationTestBase(ApiWebApplicationFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
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
}