using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace ProductManagementSystem.Api.Data;

public static class DbInitializer
{
    public static class Roles
    {
        public const string Capturer = "Capturer";
        public const string Manager = "Manager";
    }

    public static class DemoUsers
    {
        public const string CapturerEmail = "capturer@demo.local";
        public const string ManagerEmail = "manager@demo.local";
    }

    
    private const string DevFallbackPassword = "Demo123!";

    public static async Task SeedAsync(
        IServiceProvider services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var seedPassword = configuration["Seed:Password"];

        if (string.IsNullOrWhiteSpace(seedPassword))
        {
            if (!environment.IsDevelopment())
                throw new InvalidOperationException(
                    "Seed:Password must be set via the Seed__Password environment variable in non-development environments.");

            seedPassword = DevFallbackPassword;
        }

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();

        foreach (var role in new[] { Roles.Capturer, Roles.Manager })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        await SeedUserAsync(userManager, DemoUsers.CapturerEmail, seedPassword, Roles.Capturer);
        await SeedUserAsync(userManager, DemoUsers.ManagerEmail, seedPassword, Roles.Manager);
    }

    private static async Task SeedUserAsync(UserManager<IdentityUser> userManager, string email, string password, string role)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is not null)
        {
            return;
        }

        user = new IdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to seed demo user '{email}': {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }

        await userManager.AddToRoleAsync(user, role);
    }
}
