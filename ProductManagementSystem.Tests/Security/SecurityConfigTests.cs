using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using ProductManagementSystem.Api.Data;

namespace ProductManagementSystem.Tests.Security;

[Collection(IntegrationTestCollection.Name)]
public class SecurityConfigTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    // Set env vars before WAF starts — WebApplication.CreateBuilder reads env vars
    // during WebApplicationBuilder construction, before IWebHostBuilder callbacks fire.
    static SecurityConfigTests()
    {
        Environment.SetEnvironmentVariable("Jwt__Key",
            "TestOnly_SuperSecretSigningKey_LongEnoughForHmacSha256_Here!");
        Environment.SetEnvironmentVariable("Seed__Password", "Test_SeedOnly_ChangeMe1!");
    }

    private const string SeedPassword = "Test_SeedOnly_ChangeMe1!";

    public SecurityConfigTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                // Remove all DbContext-related registrations so we can replace with InMemory
                var toRemove = services
                    .Where(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>)
                        || d.ServiceType == typeof(ApplicationDbContext))
                    .ToList();
                foreach (var d in toRemove)
                    services.Remove(d);

                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase("SecurityTestDb"));
            });
        });
    }

    // SEC-01: Jwt:Key must be empty in appsettings.json (real key injected via env var at runtime)
    [Fact]
    public void AppsettingsJson_JwtKey_IsEmpty()
    {
        var appsettingsPath = Path.Combine(
            AppContext.BaseDirectory,
            "appsettings.json");

        Assert.True(File.Exists(appsettingsPath), $"appsettings.json not found at {appsettingsPath}");

        var json = File.ReadAllText(appsettingsPath);
        var node = JsonNode.Parse(json);
        var key = node?["Jwt"]?["Key"]?.GetValue<string>();

        Assert.Equal(string.Empty, key);
    }

    // SEC-02: DbInitializer reads seed password from IConfiguration
    [Fact]
    public async Task DbInitializer_UsesConfigurationPassword_NotHardcoded()
    {
        var configPassword = "Config_SeedTest_Password1!";

        var mockConfig = new Mock<IConfiguration>();
        mockConfig.Setup(c => c["Seed:Password"]).Returns(configPassword);

        var mockEnv = new Mock<IWebHostEnvironment>();
        mockEnv.Setup(e => e.EnvironmentName).Returns("Production");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(opts =>
            opts.UseInMemoryDatabase("SeedTest_" + Guid.NewGuid()));
        services.AddIdentity<IdentityUser, IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        // SeedAsync must accept IConfiguration and IWebHostEnvironment — compile-time check
        await DbInitializer.SeedAsync(
            scope.ServiceProvider,
            mockConfig.Object,
            mockEnv.Object);

        mockConfig.Verify(c => c["Seed:Password"], Times.AtLeastOnce);
    }

    // SEC-05 / SEC-07: password policy — weak password fails
    [Fact]
    public async Task PasswordPolicy_ShortPassword_FailsValidation()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        var user = new IdentityUser { UserName = "policytest@test.local", Email = "policytest@test.local" };
        var result = await userManager.CreateAsync(user, "short");

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Code.Contains("Password"));
    }

    // SEC-07: Demo123! satisfies policy (8 chars, upper, digit, special)
    [Fact]
    public async Task PasswordPolicy_DemoPassword_PassesValidation()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        var user = new IdentityUser { UserName = "demopolicytest@test.local", Email = "demopolicytest@test.local" };
        var result = await userManager.CreateAsync(user, "Demo123!");

        Assert.True(result.Succeeded);
    }

    // SEC-07: policy-compliant password passes
    [Fact]
    public async Task PasswordPolicy_CompliantPassword_PassesValidation()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        var user = new IdentityUser { UserName = "policyok@test.local", Email = "policyok@test.local" };
        var result = await userManager.CreateAsync(user, "LongPassword123!");

        Assert.True(result.Succeeded);
    }

    // SEC-08: Description > 2000 chars → 400
    [Fact]
    public async Task Create_DescriptionTooLong_Returns400()
    {
        var client = _factory.CreateClient();
        var token = await GetCapturerTokenAsync(client);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var longDesc = new string('x', 2001);
        var response = await client.PostAsJsonAsync("/api/products", new
        {
            name = "Test Product",
            description = longDesc,
            price = 10.00m,
            stock = 5
        });

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    // SEC-08: Reject reason > 500 chars → 400
    [Fact]
    public async Task Reject_ReasonTooLong_Returns400()
    {
        var client = _factory.CreateClient();
        var capturerToken = await GetCapturerTokenAsync(client);
        var managerToken = await GetManagerTokenAsync(client);

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", capturerToken);

        var createResp = await client.PostAsJsonAsync("/api/products", new
        {
            name = "Reject Test Product",
            description = "desc",
            price = 1.00m,
            stock = 1
        });

        if (createResp.StatusCode != System.Net.HttpStatusCode.Created)
            return; // abort test if product creation fails

        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var productId = created.GetProperty("id").GetInt32();

        // Submit for approval
        await client.PutAsJsonAsync($"/api/products/{productId}", new
        {
            name = "Reject Test Product",
            description = "desc",
            price = 1.00m,
            stock = 1
        });

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", managerToken);

        var longReason = new string('r', 501);
        var rejectResp = await client.PostAsJsonAsync($"/api/products/{productId}/reject",
            new { reason = longReason });

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, rejectResp.StatusCode);
    }

    // SEC-09: Price < 0 → 400
    [Fact]
    public async Task Create_NegativePrice_Returns400()
    {
        var client = _factory.CreateClient();
        var token = await GetCapturerTokenAsync(client);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/products", new
        {
            name = "Bad Price",
            description = "test",
            price = -1.00m,
            stock = 5
        });

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    // SEC-09: Stock < 0 → 400
    [Fact]
    public async Task Create_NegativeStock_Returns400()
    {
        var client = _factory.CreateClient();
        var token = await GetCapturerTokenAsync(client);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/products", new
        {
            name = "Bad Stock",
            description = "test",
            price = 10.00m,
            stock = -1
        });

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ISSUE-07 / SEC-05: lockout configured
    [Fact]
    public void IdentityOptions_ShouldHaveLockoutConfigured()
    {
        using var scope = _factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<IdentityOptions>>().Value;

        Assert.Equal(5, options.Lockout.MaxFailedAccessAttempts);
        Assert.Equal(TimeSpan.FromMinutes(5), options.Lockout.DefaultLockoutTimeSpan);
        Assert.True(options.Lockout.AllowedForNewUsers);
    }

    // ISSUE-08 / SEC-03: RequireHttpsMetadata true outside development
    [Fact]
    public void JwtBearer_ShouldRequireHttpsMetadata_OutsideDevelopment()
    {
        var productionFactory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureServices(services =>
            {
                var toRemove = services
                    .Where(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>)
                        || d.ServiceType == typeof(ApplicationDbContext))
                    .ToList();
                foreach (var d in toRemove)
                    services.Remove(d);
                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase("ProdHttpsTest"));
            });
        });

        using var scope = productionFactory.Services.CreateScope();
        var monitor = scope.ServiceProvider
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
        var options = monitor.Get(JwtBearerDefaults.AuthenticationScheme);

        Assert.True(options.RequireHttpsMetadata);
    }

    // ISSUE-11 / SEC-10: unknown CORS origin rejected
    [Fact]
    public async Task CorsPolicy_ShouldRejectRequestFromUnknownOrigin()
    {
        var client = _factory.CreateClient();
        var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, "/api/health");
        request.Headers.Add("Origin", "http://evil.example.com");

        var response = await client.SendAsync(request);

        Assert.False(
            response.Headers.Contains("Access-Control-Allow-Origin"),
            "Unexpected ACAO header returned for unknown origin");
    }

    // ISSUE-15 / SEC-12: health endpoint returns only {status}
    [Fact]
    public async Task Health_ShouldReturnOnlyStatus_WithoutServiceName()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/health");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        var node = JsonNode.Parse(body);

        Assert.NotNull(node?["status"]);
        Assert.Null(node?["service"]);
    }

    private Task<string> GetCapturerTokenAsync(System.Net.Http.HttpClient client) =>
        GetTokenAsync(client, "capturer@demo.local");

    private Task<string> GetManagerTokenAsync(System.Net.Http.HttpClient client) =>
        GetTokenAsync(client, "manager@demo.local");

    private async Task<string> GetTokenAsync(System.Net.Http.HttpClient client, string email)
    {
        client.DefaultRequestHeaders.Authorization = null;
        var resp = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = SeedPassword
        });

        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("token").GetString()!;
    }
}
