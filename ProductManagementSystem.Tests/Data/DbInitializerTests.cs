using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ProductManagementSystem.Api.Data;

namespace ProductManagementSystem.Tests.Data;

public class DbInitializerTests
{
    private static ServiceProvider BuildServices(string envName, string? seedPassword)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(o =>
            o.UseInMemoryDatabase("DbInitTest_" + Guid.NewGuid()));
        services.AddIdentity<IdentityUser, IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        var configBuilder = new ConfigurationBuilder();
        if (seedPassword is not null)
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?> { ["Seed:Password"] = seedPassword });
        services.AddSingleton<IConfiguration>(configBuilder.Build());

        var mockEnv = new Mock<IWebHostEnvironment>();
        mockEnv.Setup(e => e.EnvironmentName).Returns(envName);
        services.AddSingleton<IWebHostEnvironment>(mockEnv.Object);

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task SeedAsync_ShouldThrow_WhenPasswordMissingAndNotDevelopment()
    {
        await using var provider = BuildServices("Production", seedPassword: null);
        using var scope = provider.CreateScope();

        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DbInitializer.SeedAsync(scope.ServiceProvider, config, env));
    }

    [Fact]
    public async Task SeedAsync_ShouldUseFallback_WhenPasswordMissingInDevelopment()
    {
        await using var provider = BuildServices("Development", seedPassword: null);
        using var scope = provider.CreateScope();

        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

        var ex = await Record.ExceptionAsync(() =>
            DbInitializer.SeedAsync(scope.ServiceProvider, config, env));
        Assert.Null(ex);

        // Verify seed completed — user should exist
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = await userManager.FindByEmailAsync(DbInitializer.DemoUsers.CapturerEmail);
        Assert.NotNull(user);
    }
}
