using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductManagementSystem.Api.Controllers;
using ProductManagementSystem.Api.Data;
using ProductManagementSystem.Api.Models;
using ProductManagementSystem.Api.Services;

namespace ProductManagementSystem.Tests.Performance;

[Trait("Category", "Performance")]
public class PaginationTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly ProductsController _sut;

    public PaginationTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);
        var service = new ProductService(_db);
        _sut = new ProductsController(service);

        // Wire up a manager identity so all products are visible
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "manager1"),
            new Claim(ClaimTypes.Email, "manager@test.com"),
            new Claim(ClaimTypes.Role, DbInitializer.Roles.Manager),
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    public void Dispose() => _db.Dispose();

    // --- GetAll pagination ---

    [Fact]
    public async Task GetAll_DefaultParams_ReturnsMax50Items()
    {
        SeedProducts(60);
        await _db.SaveChangesAsync();

        var result = await _sut.GetAll();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<IEnumerable<ProductsController.ProductResponse>>(ok.Value);
        Assert.Equal(50, items.Count());
    }

    [Fact]
    public async Task GetAll_PageSize5_Returns5Items()
    {
        SeedProducts(10);
        await _db.SaveChangesAsync();

        var result = await _sut.GetAll(page: 1, pageSize: 5);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<IEnumerable<ProductsController.ProductResponse>>(ok.Value);
        Assert.Equal(5, items.Count());
    }

    [Fact]
    public async Task GetAll_Page2PageSize2_ReturnsCorrectSkip()
    {
        // Seed 4 products with distinct UpdatedAt to make ordering deterministic
        var base_time = DateTime.UtcNow;
        for (var i = 0; i < 4; i++)
        {
            _db.Products.Add(new Product
            {
                Name = $"Product {i + 1}",
                Price = 1m,
                Stock = 1,
                Status = ProductStatus.Draft,
                CreatedBy = "manager1",
                UpdatedBy = "manager1",
                CreatedAt = base_time,
                UpdatedAt = base_time.AddMinutes(i)
            });
        }
        await _db.SaveChangesAsync();

        var page1 = await _sut.GetAll(page: 1, pageSize: 2);
        var page2 = await _sut.GetAll(page: 2, pageSize: 2);

        var ok1 = Assert.IsType<OkObjectResult>(page1.Result);
        var ok2 = Assert.IsType<OkObjectResult>(page2.Result);
        var items1 = Assert.IsAssignableFrom<IEnumerable<ProductsController.ProductResponse>>(ok1.Value).ToList();
        var items2 = Assert.IsAssignableFrom<IEnumerable<ProductsController.ProductResponse>>(ok2.Value).ToList();

        Assert.Equal(2, items1.Count);
        Assert.Equal(2, items2.Count);
        // Pages must not overlap
        Assert.Empty(items1.Select(i => i.Id).Intersect(items2.Select(i => i.Id)));
    }

    [Fact]
    public async Task GetAll_ResponseItems_HaveEmptyHistory()
    {
        var p = SeedProductWithHistory("manager1");
        await _db.SaveChangesAsync();

        var result = await _sut.GetAll(page: 1, pageSize: 10);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<IEnumerable<ProductsController.ProductResponse>>(ok.Value).ToList();
        Assert.Single(items);
        Assert.Empty(items[0].History);
    }

    // --- GetApproved pagination ---

    [Fact]
    public async Task GetApproved_PageSize10_ReturnsMax10Items()
    {
        SeedApprovedCache(15);
        await _db.SaveChangesAsync();

        var result = await _sut.GetApproved(page: 1, pageSize: 10);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<IEnumerable<ProductsController.ApprovedResponse>>(ok.Value);
        Assert.Equal(10, items.Count());
    }

    [Fact]
    public async Task GetApproved_PageSize200_ClampsTo100()
    {
        SeedApprovedCache(150);
        await _db.SaveChangesAsync();

        var result = await _sut.GetApproved(page: 1, pageSize: 200);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<IEnumerable<ProductsController.ApprovedResponse>>(ok.Value);
        Assert.Equal(100, items.Count());
    }

    // --- helpers ---

    private void SeedProducts(int count)
    {
        var now = DateTime.UtcNow;
        for (var i = 0; i < count; i++)
        {
            _db.Products.Add(new Product
            {
                Name = $"Product {i + 1}",
                Price = 1m,
                Stock = 1,
                Status = ProductStatus.Draft,
                CreatedBy = "manager1",
                UpdatedBy = "manager1",
                CreatedAt = now,
                UpdatedAt = now
            });
        }
    }

    private Product SeedProductWithHistory(string createdBy)
    {
        var now = DateTime.UtcNow;
        var p = new Product
        {
            Name = "With History",
            Price = 1m,
            Stock = 1,
            Status = ProductStatus.Draft,
            CreatedBy = createdBy,
            UpdatedBy = createdBy,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.Products.Add(p);
        p.History.Add(new ProductApprovalHistory
        {
            Action = "Created",
            ActorId = createdBy,
            ActorName = "Manager",
            Timestamp = now
        });
        return p;
    }

    private void SeedApprovedCache(int count)
    {
        var now = DateTime.UtcNow;
        for (var i = 0; i < count; i++)
        {
            _db.ApprovedProductsCache.Add(new ApprovedProductsCache
            {
                ProductId = i + 1,
                Name = $"Approved {i + 1}",
                Price = 1m,
                Stock = 1,
                ApprovedAt = now.AddMinutes(i),
                ApprovedBy = "manager"
            });
        }
    }
}
