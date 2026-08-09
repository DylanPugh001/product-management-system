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

    // spidersense: dev fallback only, never used in production
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
        if (user is null)
        {
            user = new IdentityUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };

            var createResult = await userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to seed demo user '{email}': {string.Join("; ", createResult.Errors.Select(e => e.Description))}");
            }

            await userManager.AddToRoleAsync(user, role);
            return;
        }

        var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        var resetResult = await userManager.ResetPasswordAsync(user, resetToken, password);
        if (!resetResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to reset demo password for '{email}': {string.Join("; ", resetResult.Errors.Select(e => e.Description))}");
        }

        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            await userManager.UpdateAsync(user);
        }

        var roles = await userManager.GetRolesAsync(user);
        if (!roles.Contains(role))
        {
            await userManager.AddToRoleAsync(user, role);
        }
    }
}
