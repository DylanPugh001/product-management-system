using Microsoft.EntityFrameworkCore;
using ProductManagementSystem.Api.Data;
using ProductManagementSystem.Api.Models;
using ProductManagementSystem.Api.Services;

namespace ProductManagementSystem.Tests.Services;

public class ProductServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly ProductService _sut;

    public ProductServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);
        _sut = new ProductService(_db);
    }

    public void Dispose() => _db.Dispose();

    // --- GetAllAsync ---

    [Fact]
    public async Task GetAllAsync_Manager_ReturnsAllProducts()
    {
        var p1 = SeedProduct("u1", ProductStatus.Draft);
        var p2 = SeedProduct("u2", ProductStatus.Approved);
        await _db.SaveChangesAsync();

        var result = await _sut.GetAllAsync("manager", isManager: true);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetAllAsync_Capturer_SeesOwnAndApprovedOnly()
    {
        var own = SeedProduct("capturer1", ProductStatus.Draft);
        var approved = SeedProduct("capturer2", ProductStatus.Approved);
        var otherDraft = SeedProduct("capturer2", ProductStatus.Draft);
        await _db.SaveChangesAsync();

        var result = await _sut.GetAllAsync("capturer1", isManager: false);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.Id == own.Id);
        Assert.Contains(result, p => p.Id == approved.Id);
        Assert.DoesNotContain(result, p => p.Id == otherDraft.Id);
    }

    // --- CreateAsync ---

    [Fact]
    public async Task CreateAsync_CreatesProductWithDraftStatus()
    {
        var request = new ProductRequest("Widget", "A widget", 9.99m, 100);

        var product = await _sut.CreateAsync(request, "user1", "User One");

        Assert.Equal(ProductStatus.Draft, product.Status);
        Assert.Equal("Widget", product.Name);
        Assert.Equal("user1", product.CreatedBy);
    }

    [Fact]
    public async Task CreateAsync_CreatesHistoryRecord()
    {
        var request = new ProductRequest("Widget", null, 1m, 1);

        var product = await _sut.CreateAsync(request, "user1", "User One");

        var history = await _db.ProductApprovalHistory
            .Where(h => h.ProductId == product.Id)
            .ToListAsync();
        Assert.Single(history);
        Assert.Equal("Created", history[0].Action);
    }

    [Fact]
    public async Task CreateAsync_BothSavesCommitted_ProductAndHistoryPersisted()
    {
        var request = new ProductRequest("Widget", null, 1m, 1);

        var product = await _sut.CreateAsync(request, "user1", "User One");

        var dbProduct = await _db.Products.FindAsync(product.Id);
        var historyCount = await _db.ProductApprovalHistory.CountAsync(h => h.ProductId == product.Id);
        Assert.NotNull(dbProduct);
        Assert.Equal(1, historyCount);
    }

    // --- UpdateAsync ---

    [Fact]
    public async Task UpdateAsync_ReturnsNull_WhenNotFound()
    {
        var result = await _sut.UpdateAsync(999, new ProductRequest("X", null, 1m, 1), "u1", "U1");

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_Throws_WhenNotOwner()
    {
        var p = SeedProduct("owner", ProductStatus.Draft);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.UpdateAsync(p.Id, new ProductRequest("X", null, 1m, 1), "other", "Other"));
    }

    [Fact]
    public async Task UpdateAsync_Throws_WhenSoftDeleted()
    {
        var p = SeedProduct("owner", ProductStatus.SoftDeleted);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.UpdateAsync(p.Id, new ProductRequest("X", null, 1m, 1), "owner", "Owner"));
    }

    [Fact]
    public async Task UpdateAsync_RecordsHistory()
    {
        var p = SeedProduct("owner", ProductStatus.Draft);
        await _db.SaveChangesAsync();

        await _sut.UpdateAsync(p.Id, new ProductRequest("New Name", null, 2m, 5), "owner", "Owner");

        var history = await _db.ProductApprovalHistory.Where(h => h.ProductId == p.Id).ToListAsync();
        Assert.Single(history);
        Assert.Equal("Submitted", history[0].Action);
    }

    [Fact]
    public async Task UpdateAsync_ExistingApproved_RecordsUpdateRequestedHistory()
    {
        var p = SeedProduct("owner", ProductStatus.Approved);
        await _db.SaveChangesAsync();

        await _sut.UpdateAsync(p.Id, new ProductRequest("New Name", null, 2m, 5), "owner", "Owner");

        var history = await _db.ProductApprovalHistory.Where(h => h.ProductId == p.Id).ToListAsync();
        Assert.Single(history);
        Assert.Equal("UpdateRequested", history[0].Action);
    }

    // --- ApproveAsync ---

    [Fact]
    public async Task ApproveAsync_ReturnsNull_WhenNotFound()
    {
        var result = await _sut.ApproveAsync(999, "manager", "Manager");

        Assert.Null(result);
    }

    [Fact]
    public async Task ApproveAsync_Throws_WhenNotPendingApproval()
    {
        var p = SeedProduct("owner", ProductStatus.Draft);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ApproveAsync(p.Id, "manager", "Manager"));
    }

    [Fact]
    public async Task ApproveAsync_Throws_WhenSelfApproval()
    {
        var p = SeedProduct("manager1", ProductStatus.PendingApproval);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.ApproveAsync(p.Id, "manager1", "Manager1"));
    }

    [Fact]
    public async Task ApproveAsync_PendingDelete_TransitionsToSoftDeleted()
    {
        var p = SeedProduct("owner", ProductStatus.PendingApproval, pendingDelete: true);
        await _db.SaveChangesAsync();

        var result = await _sut.ApproveAsync(p.Id, "manager", "Manager");

        Assert.NotNull(result);
        Assert.Equal(ProductStatus.SoftDeleted, result!.Status);
        Assert.False(result.PendingDelete);
    }

    [Fact]
    public async Task ApproveAsync_PendingDelete_RemovesCacheRow()
    {
        var p = SeedProduct("owner", ProductStatus.PendingApproval, pendingDelete: true);
        await _db.SaveChangesAsync();
        _db.ApprovedProductsCache.Add(new ApprovedProductsCache
        {
            ProductId = p.Id,
            Name = p.Name,
            Price = p.Price,
            Stock = p.Stock,
            ApprovedAt = DateTime.UtcNow,
            ApprovedBy = "manager"
        });
        await _db.SaveChangesAsync();

        await _sut.ApproveAsync(p.Id, "manager", "Manager");

        var cache = await _db.ApprovedProductsCache.FirstOrDefaultAsync(c => c.ProductId == p.Id);
        Assert.Null(cache);
    }

    [Fact]
    public async Task ApproveAsync_Normal_TransitionsToApproved()
    {
        var p = SeedProduct("owner", ProductStatus.PendingApproval);
        await _db.SaveChangesAsync();

        var result = await _sut.ApproveAsync(p.Id, "manager", "Manager");

        Assert.NotNull(result);
        Assert.Equal(ProductStatus.Approved, result!.Status);
    }

    [Fact]
    public async Task ApproveAsync_Normal_UpsertsCache()
    {
        var p = SeedProduct("owner", ProductStatus.PendingApproval);
        await _db.SaveChangesAsync();

        await _sut.ApproveAsync(p.Id, "manager", "Manager");

        var cache = await _db.ApprovedProductsCache.FirstOrDefaultAsync(c => c.ProductId == p.Id);
        Assert.NotNull(cache);
        Assert.Equal(p.Name, cache!.Name);
    }

    // --- RejectAsync ---

    [Fact]
    public async Task RejectAsync_ReturnsNull_WhenNotFound()
    {
        var result = await _sut.RejectAsync(999, null, "manager", "Manager");

        Assert.Null(result);
    }

    [Fact]
    public async Task RejectAsync_Throws_WhenNotPendingApproval()
    {
        var p = SeedProduct("owner", ProductStatus.Draft);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.RejectAsync(p.Id, null, "manager", "Manager"));
    }

    [Fact]
    public async Task RejectAsync_TransitionsToDraft()
    {
        var p = SeedProduct("owner", ProductStatus.PendingApproval);
        await _db.SaveChangesAsync();

        var result = await _sut.RejectAsync(p.Id, "Not good", "manager", "Manager");

        Assert.NotNull(result);
        Assert.Equal(ProductStatus.Draft, result!.Status);
        Assert.False(result.PendingDelete);
    }

    // --- RequestDeleteAsync ---

    [Fact]
    public async Task RequestDeleteAsync_ReturnsNull_WhenNotFound()
    {
        var result = await _sut.RequestDeleteAsync(999, "manager", "Manager");

        Assert.Null(result);
    }

    [Fact]
    public async Task RequestDeleteAsync_Throws_WhenAlreadySoftDeleted()
    {
        var p = SeedProduct("owner", ProductStatus.SoftDeleted);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.RequestDeleteAsync(p.Id, "manager", "Manager"));
    }

    [Fact]
    public async Task RequestDeleteAsync_Throws_WhenAlreadyPendingDelete()
    {
        var p = SeedProduct("owner", ProductStatus.Approved, pendingDelete: true);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.RequestDeleteAsync(p.Id, "manager", "Manager"));
    }

    [Fact]
    public async Task RequestDeleteAsync_SetsPendingDeleteTrue()
    {
        var p = SeedProduct("owner", ProductStatus.Approved);
        await _db.SaveChangesAsync();

        var result = await _sut.RequestDeleteAsync(p.Id, "manager", "Manager");

        Assert.NotNull(result);
        Assert.True(result!.PendingDelete);
        Assert.Equal(ProductStatus.PendingApproval, result.Status);
    }

    // --- GetAllAsync pagination / history exclusion ---

    [Fact]
    public async Task GetAllAsync_ShouldNotReturnHistoryEntries()
    {
        var p = SeedProduct("u1", ProductStatus.Draft);
        await _db.SaveChangesAsync();
        _db.ProductApprovalHistory.AddRange(
            new ProductApprovalHistory { ProductId = p.Id, Action = "A", ActorId = "u1", ActorName = "U1", Timestamp = DateTime.UtcNow },
            new ProductApprovalHistory { ProductId = p.Id, Action = "B", ActorId = "u1", ActorName = "U1", Timestamp = DateTime.UtcNow },
            new ProductApprovalHistory { ProductId = p.Id, Action = "C", ActorId = "u1", ActorName = "U1", Timestamp = DateTime.UtcNow }
        );
        await _db.SaveChangesAsync();

        var result = await _sut.GetAllAsync("u1", isManager: true);

        Assert.All(result, p => Assert.Empty(p.History));
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnPagedResults()
    {
        for (var i = 0; i < 10; i++) SeedProduct($"u{i}", ProductStatus.Draft);
        await _db.SaveChangesAsync();

        var result = await _sut.GetAllAsync("manager", isManager: true, page: 1, pageSize: 3);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnSecondPage()
    {
        for (var i = 0; i < 10; i++) SeedProduct($"u{i}", ProductStatus.Draft);
        await _db.SaveChangesAsync();

        var result = await _sut.GetAllAsync("manager", isManager: true, page: 2, pageSize: 3);

        Assert.Equal(3, result.Count);
    }

    // --- GetApprovedAsync pagination ---

    [Fact]
    public async Task GetApprovedAsync_ShouldRespectPageSize()
    {
        var now = DateTime.UtcNow;
        for (var i = 0; i < 5; i++)
        {
            _db.ApprovedProductsCache.Add(new ApprovedProductsCache
            {
                ProductId = i + 1,
                Name = $"P{i}",
                Price = 1m,
                Stock = 1,
                ApprovedAt = now.AddMinutes(i),
                ApprovedBy = "manager"
            });
        }
        await _db.SaveChangesAsync();

        var result = await _sut.GetApprovedAsync(page: 1, pageSize: 2);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetApprovedAsync_ShouldCapPageSizeAt100()
    {
        var now = DateTime.UtcNow;
        for (var i = 0; i < 10; i++)
        {
            _db.ApprovedProductsCache.Add(new ApprovedProductsCache
            {
                ProductId = i + 1,
                Name = $"P{i}",
                Price = 1m,
                Stock = 1,
                ApprovedAt = now.AddMinutes(i),
                ApprovedBy = "manager"
            });
        }
        await _db.SaveChangesAsync();

        var result = await _sut.GetApprovedAsync(page: 1, pageSize: 999);

        Assert.True(result.Count <= 100);
    }

    // --- GetByIdAsync history ordering ---

    [Fact]
    public async Task GetByIdAsync_ShouldReturnHistoryInDescendingOrder()
    {
        var p = SeedProduct("u1", ProductStatus.Draft);
        await _db.SaveChangesAsync();
        var base_time = DateTime.UtcNow;
        _db.ProductApprovalHistory.AddRange(
            new ProductApprovalHistory { ProductId = p.Id, Action = "First",  ActorId = "u1", ActorName = "U1", Timestamp = base_time },
            new ProductApprovalHistory { ProductId = p.Id, Action = "Second", ActorId = "u1", ActorName = "U1", Timestamp = base_time.AddMinutes(1) },
            new ProductApprovalHistory { ProductId = p.Id, Action = "Third",  ActorId = "u1", ActorName = "U1", Timestamp = base_time.AddMinutes(2) }
        );
        await _db.SaveChangesAsync();

        var result = await _sut.GetByIdAsync(p.Id);

        Assert.NotNull(result);
        var history = result!.History.ToList();
        Assert.Equal(3, history.Count);
        Assert.True(history[0].Timestamp >= history[1].Timestamp);
        Assert.True(history[1].Timestamp >= history[2].Timestamp);
    }

    // --- Input validation (ISSUE-09 / SEC-09, ISSUE-10 / SEC-08) ---

    [Fact]
    public async Task CreateAsync_ShouldReturnBadRequest_WhenPriceIsNegative()
    {
        var request = new ProductRequest("Widget", "desc", -1m, 1);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.CreateAsync(request, "user1", "User One"));
    }

    [Fact]
    public async Task CreateAsync_ShouldReturnBadRequest_WhenStockIsNegative()
    {
        var request = new ProductRequest("Widget", "desc", 1m, -1);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.CreateAsync(request, "user1", "User One"));
    }

    [Fact]
    public async Task UpdateAsync_ShouldReturnBadRequest_WhenPriceIsNegative()
    {
        var p = SeedProduct("owner", ProductStatus.Draft);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.UpdateAsync(p.Id, new ProductRequest("Widget", "desc", -1m, 1), "owner", "Owner"));
    }

    [Fact]
    public async Task CreateAsync_ShouldReturnBadRequest_WhenDescriptionExceeds2000Chars()
    {
        var longDesc = new string('x', 2001);
        var request = new ProductRequest("Widget", longDesc, 1m, 1);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.CreateAsync(request, "user1", "User One"));
    }

    [Fact]
    public async Task RejectAsync_ShouldReturnBadRequest_WhenReasonExceeds500Chars()
    {
        var longReason = new string('r', 501);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.RejectAsync(999, longReason, "manager", "Manager"));
    }

    // --- helpers ---

    private Product SeedProduct(string createdBy, ProductStatus status, bool pendingDelete = false)
    {
        var p = new Product
        {
            Name = "Test Product",
            Price = 1m,
            Stock = 1,
            Status = status,
            CreatedBy = createdBy,
            UpdatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            PendingDelete = pendingDelete
        };
        _db.Products.Add(p);
        return p;
    }
}
