using Microsoft.EntityFrameworkCore;
using ProductManagementSystem.Api.Data;
using ProductManagementSystem.Api.Models;

namespace ProductManagementSystem.Tests.Data;

public class SchemaTests
{
    [Fact]
    public void ApplicationDbContext_ShouldHaveIndexOnProductsCreatedBy()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var db = new ApplicationDbContext(options);

        var entityType = db.Model.FindEntityType(typeof(Product))!;
        var indexes = entityType.GetIndexes();
        var hasCreatedByIndex = indexes.Any(i =>
            i.Properties.Any(p => p.Name == nameof(Product.CreatedBy)));

        Assert.True(hasCreatedByIndex, "Products should have an index on CreatedBy");
    }
}
