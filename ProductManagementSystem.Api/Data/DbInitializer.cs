using Microsoft.AspNetCore.Identity;

namespace ProductManagementSystem.Api.Data;

public static class DbInitializer
{
    public static class Roles
    {
        public const string Capturer = "Capturer";
        public const string Manager = "Manager";
    }

    public static async Task SeedAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        foreach (var role in new[] { Roles.Capturer, Roles.Manager })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }
}
