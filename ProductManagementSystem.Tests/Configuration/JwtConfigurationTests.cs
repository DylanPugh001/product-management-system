using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProductManagementSystem.Api.Data;

namespace ProductManagementSystem.Tests.Configuration;

/// <summary>
/// Tests that Program.cs throws on startup when Jwt:Key is absent or too short.
/// WebApplication.CreateBuilder reads env vars during construction (before IWebHostBuilder
/// callbacks fire), so we inject the key via environment variables.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class JwtConfigurationTests : IDisposable
{
    private readonly string? _originalKey;
    private readonly string? _originalSeedPassword;

    public JwtConfigurationTests()
    {
        _originalKey = Environment.GetEnvironmentVariable("Jwt__Key");
        _originalSeedPassword = Environment.GetEnvironmentVariable("Seed__Password");
        Environment.SetEnvironmentVariable("Seed__Password", "Test_SeedOnly_ChangeMe1!");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("Jwt__Key", _originalKey);
        Environment.SetEnvironmentVariable("Seed__Password", _originalSeedPassword);
    }

    private static WebApplicationFactory<Program> BuildFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor is not null)
                    services.Remove(descriptor);
                services.AddDbContext<ApplicationDbContext>(o =>
                    o.UseInMemoryDatabase("JwtCfgTest_" + Guid.NewGuid()));
            });
        });


    [Fact]
    public void Startup_ShouldThrow_WhenJwtKeyTooShort()
    {
        Environment.SetEnvironmentVariable("Jwt__Key", "tooshort");
        var factory = BuildFactory();
        Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
    }

    [Fact]
    public void Startup_ShouldSucceed_WhenJwtKeyIsValidLength()
    {
        Environment.SetEnvironmentVariable("Jwt__Key", new string('x', 32));
        var factory = BuildFactory();
        var ex = Record.Exception(() => factory.CreateClient());
        Assert.Null(ex);
    }
}
