using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ProductManagementSystem.Api.Data;

/// <summary>
/// Design-time factory used by `dotnet ef` so migrations are always generated
/// against the SQL Server provider without executing Program.cs startup code
/// (which would otherwise try to connect to a database at design time).
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=tcp:placeholder.database.windows.net,1433;Database=productmanagement;User Id=sa;Password=placeholder;TrustServerCertificate=True;");
        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
