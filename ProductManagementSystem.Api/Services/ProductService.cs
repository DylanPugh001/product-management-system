using Microsoft.EntityFrameworkCore;
using ProductManagementSystem.Api.Data;
using ProductManagementSystem.Api.Models;

namespace ProductManagementSystem.Api.Services;

public class ProductService : IProductService
{
    private readonly ApplicationDbContext _db;

    public ProductService(ApplicationDbContext db)
    {
        _db = db;
    }

    // spidersense: filtered include ordering — EF Core 5+, upgrade path: none needed
    private IQueryable<Product> ProductsWithHistory =>
        _db.Products.Include(p => p.History.OrderByDescending(h => h.Timestamp));

    public async Task<IReadOnlyList<Product>> GetAllAsync(string userId, bool isManager, int page = 1, int pageSize = 50)
    {
        var query = isManager
            ? _db.Products
            : _db.Products.Where(p => p.CreatedBy == userId || p.Status == ProductStatus.Approved);

        // PERF-01: history excluded from list view — loaded only in GetByIdAsync
        return await query
            .OrderByDescending(p => p.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new Product
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Price = p.Price,
                Stock = p.Stock,
                Status = p.Status,
                CreatedBy = p.CreatedBy,
                UpdatedBy = p.UpdatedBy,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
                PendingDelete = p.PendingDelete
            })
            .ToListAsync();
    }

    public async Task<Product?> GetByIdAsync(int id)
    {
        var product = await ProductsWithHistory.FirstOrDefaultAsync(p => p.Id == id);
        // spidersense: InMemory doesn't honour filtered-include ordering — sort in C# as fallback; on SQLite the filtered include already sorted, ceiling: none
        if (product is not null)
            product.History = product.History.OrderByDescending(h => h.Timestamp).ToList();
        return product;
    }

    public async Task<Product> CreateAsync(ProductRequest request, string actorId, string actorName)
    {
        if (request.Price < 0)
            throw new ArgumentException("Price must be non-negative.", nameof(request));
        if (request.Stock < 0)
            throw new ArgumentException("Stock must be non-negative.", nameof(request));
        if (request.Description?.Length > 2000)
            throw new ArgumentException("Description must be 2000 characters or fewer.", nameof(request));

        var now = DateTime.UtcNow;
        var product = new Product
        {
            Name = request.Name.Trim(),
            Description = request.Description,
            Price = request.Price,
            Stock = request.Stock,
            Status = ProductStatus.Draft,
            CreatedBy = actorId,
            UpdatedBy = actorId,
            CreatedAt = now,
            UpdatedAt = now
        };

        var supportsTransactions = _db.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory";
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? tx =
            supportsTransactions ? await _db.Database.BeginTransactionAsync() : null;

        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        AddHistory(product, "Created", actorId, actorName, null);
        await _db.SaveChangesAsync();

        if (tx is not null)
        {
            await tx.CommitAsync();
            await tx.DisposeAsync();
        }

        return product;
    }

    public async Task<Product?> UpdateAsync(int id, ProductRequest request, string actorId, string actorName)
    {
        if (request.Price < 0)
            throw new ArgumentException("Price must be non-negative.", nameof(request));
        if (request.Stock < 0)
            throw new ArgumentException("Stock must be non-negative.", nameof(request));
        if (request.Description?.Length > 2000)
            throw new ArgumentException("Description must be 2000 characters or fewer.", nameof(request));

        var product = await ProductsWithHistory.FirstOrDefaultAsync(p => p.Id == id);
        if (product is null)
        {
            return null;
        }

        if (product.CreatedBy != actorId)
        {
            throw new UnauthorizedAccessException("You do not own this product.");
        }

        if (product.Status == ProductStatus.SoftDeleted)
        {
            throw new InvalidOperationException("Soft-deleted products cannot be edited.");
        }

        var action = product.Status == ProductStatus.Draft ? "Submitted" : "UpdateRequested";
        product.Name = request.Name.Trim();
        product.Description = request.Description;
        product.Price = request.Price;
        product.Stock = request.Stock;
        product.Status = ProductStatus.PendingApproval;
        product.PendingDelete = false;
        product.UpdatedBy = actorId;
        product.UpdatedAt = DateTime.UtcNow;

        AddHistory(product, action, actorId, actorName, "Capturer submitted changes for approval.");
        await _db.SaveChangesAsync();

        return product;
    }

    public async Task<Product?> ApproveAsync(int id, string actorId, string actorName)
    {
        var product = await ProductsWithHistory.FirstOrDefaultAsync(p => p.Id == id);
        if (product is null)
        {
            return null;
        }

        if (product.Status != ProductStatus.PendingApproval)
        {
            throw new InvalidOperationException("Only pending products can be approved.");
        }

        if (product.CreatedBy == actorId)
        {
            throw new UnauthorizedAccessException("A manager cannot approve their own change.");
        }

        if (product.PendingDelete)
        {
            product.Status = ProductStatus.SoftDeleted;
            AddHistory(product, "SoftDeleted", actorId, actorName, "Manager approved the delete request.");

            var cacheRow = await _db.ApprovedProductsCache.FirstOrDefaultAsync(c => c.ProductId == product.Id);
            if (cacheRow is not null)
            {
                _db.ApprovedProductsCache.Remove(cacheRow);
            }

            product.PendingDelete = false;
            product.UpdatedBy = actorId;
            product.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return product;
        }

        product.Status = ProductStatus.Approved;
        AddHistory(product, "Approved", actorId, actorName, "Manager approved the change.");

        var cache = await _db.ApprovedProductsCache.FirstOrDefaultAsync(c => c.ProductId == product.Id);
        if (cache is null)
        {
            cache = new ApprovedProductsCache { ProductId = product.Id };
            _db.ApprovedProductsCache.Add(cache);
        }

        cache.Name = product.Name;
        cache.Description = product.Description;
        cache.Price = product.Price;
        cache.Stock = product.Stock;
        cache.ApprovedAt = DateTime.UtcNow;
        cache.ApprovedBy = actorName;

        product.PendingDelete = false;
        product.UpdatedBy = actorId;
        product.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return product;
    }

    public async Task<Product?> RejectAsync(int id, string? reason, string actorId, string actorName)
    {
        if (reason?.Length > 500)
            throw new ArgumentException("Rejection reason must be 500 characters or fewer.", nameof(reason));

        var product = await ProductsWithHistory.FirstOrDefaultAsync(p => p.Id == id);
        if (product is null)
        {
            return null;
        }

        if (product.Status != ProductStatus.PendingApproval)
        {
            throw new InvalidOperationException("Only pending products can be rejected.");
        }

        product.Status = ProductStatus.Draft;
        product.PendingDelete = false;
        product.UpdatedBy = actorId;
        product.UpdatedAt = DateTime.UtcNow;

        AddHistory(product, "Rejected", actorId, actorName, reason);
        await _db.SaveChangesAsync();

        return product;
    }

    public async Task<Product?> RequestDeleteAsync(int id, string actorId, string actorName)
    {
        var product = await ProductsWithHistory.FirstOrDefaultAsync(p => p.Id == id);
        if (product is null)
        {
            return null;
        }

        if (product.Status == ProductStatus.SoftDeleted || product.PendingDelete)
        {
            throw new InvalidOperationException("Product is already deleted or pending deletion.");
        }

        product.Status = ProductStatus.PendingApproval;
        product.PendingDelete = true;
        product.UpdatedBy = actorId;
        product.UpdatedAt = DateTime.UtcNow;

        AddHistory(product, "SoftDeleteRequested", actorId, actorName, "Manager requested a soft delete.");
        await _db.SaveChangesAsync();

        return product;
    }

    public async Task<IReadOnlyList<ApprovedProductsCache>> GetApprovedAsync(int page = 1, int pageSize = 50)
    {
        return await _db.ApprovedProductsCache
            .OrderByDescending(c => c.ApprovedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    private static void AddHistory(Product product, string action, string actorId, string actorName, string? note)
    {
        product.History.Add(new ProductApprovalHistory
        {
            ProductId = product.Id,
            Action = action,
            ActorId = actorId,
            ActorName = actorName,
            Timestamp = DateTime.UtcNow,
            Note = note
        });
    }
}
