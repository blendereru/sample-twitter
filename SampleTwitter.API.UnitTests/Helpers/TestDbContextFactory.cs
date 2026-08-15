using Microsoft.EntityFrameworkCore;
using SampleTwitter.API.Data;

namespace SampleTwitter.API.UnitTests.Helpers;

public static class TestDbContextFactory
{
    public static ApplicationContext Create()
    {
        var options = new DbContextOptionsBuilder<ApplicationContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationContext(options);
    }
}