using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductManagementSystem.Api.Data;
using ProductManagementSystem.Api.Models;

namespace ProductManagementSystem.Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public ProductsController(ApplicationDbContext db)
    {
        _db = db;
    }

    public record ProductRequest(string Name, string? Description, decimal Price, int Stock);

    public record HistoryResponse(int Id, string Action, string ActorName, DateTime Timestamp, string? Note);

    public record ProductResponse(
        int Id,
        string Name,
        string? Description,
        decimal Price,
        int Stock,
        ProductStatus Status,
        string CreatedBy,
        string UpdatedBy,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        bool PendingDelete,
        ICollection<HistoryResponse> History);

    public record ApprovedResponse(int Id, int ProductId, string Name, string? Description, decimal Price, int Stock, DateTime ApprovedAt, string ApprovedBy);

    private IQueryable<Product> ProductsWithHistory => _db.Products.Include(p => p.History);

    private ProductResponse ToResponse(Product product) => new(
        product.Id,
        product.Name,
        product.Description,
        product.Price,
        product.Stock,
        product.Status,
        product.CreatedBy,
        product.UpdatedBy,
        product.CreatedAt,
        product.UpdatedAt,
        product.PendingDelete,
        product.History
            .OrderByDescending(h => h.Timestamp)
            .Select(h => new HistoryResponse(h.Id, h.Action, h.ActorName, h.Timestamp, h.Note))
            .ToList());

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    private string CurrentUserName =>
        User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue(ClaimTypes.Name) ?? CurrentUserId;

    private bool IsManager => User.IsInRole(DbInitializer.Roles.Manager);

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<IEnumerable<ProductResponse>>> GetAll()
    {
        var userId = CurrentUserId;
        var query = IsManager
            ? ProductsWithHistory
            : ProductsWithHistory.Where(p => p.CreatedBy == userId || p.Status == ProductStatus.Approved);

        var products = await query.OrderByDescending(p => p.UpdatedAt).ToListAsync();
        return Ok(products.Select(ToResponse));
    }

    [HttpGet("{id:int}")]
    [Authorize]
    public async Task<ActionResult<ProductResponse>> GetById(int id)
    {
        var product = await ProductsWithHistory.FirstOrDefaultAsync(p => p.Id == id);
        if (product is null)
        {
            return NotFound();
        }

        if (!IsManager && product.CreatedBy != CurrentUserId && product.Status != ProductStatus.Approved)
        {
            return NotFound();
        }

        return Ok(ToResponse(product));
    }

    [HttpPost]
    [Authorize(Roles = "Capturer")]
    public async Task<ActionResult<ProductResponse>> Create(ProductRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Name is required." });
        }

        var now = DateTime.UtcNow;
        var product = new Product
        {
            Name = request.Name.Trim(),
            Description = request.Description,
            Price = request.Price,
            Stock = request.Stock,
            Status = ProductStatus.Draft,
            CreatedBy = CurrentUserId,
            UpdatedBy = CurrentUserId,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        AddHistory(product, "Created", CurrentUserId, CurrentUserName, null);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = product.Id }, ToResponse(product));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Capturer")]
    public async Task<ActionResult<ProductResponse>> Update(int id, ProductRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Name is required." });
        }

        var product = await ProductsWithHistory.FirstOrDefaultAsync(p => p.Id == id);
        if (product is null)
        {
            return NotFound();
        }

        if (product.CreatedBy != CurrentUserId)
        {
            return Forbid();
        }

        if (product.Status == ProductStatus.SoftDeleted)
        {
            return BadRequest(new { message = "Soft-deleted products cannot be edited." });
        }

        var action = product.Status == ProductStatus.Draft ? "Submitted" : "UpdateRequested";
        product.Name = request.Name.Trim();
        product.Description = request.Description;
        product.Price = request.Price;
        product.Stock = request.Stock;
        product.Status = ProductStatus.PendingApproval;
        product.PendingDelete = false;
        product.UpdatedBy = CurrentUserId;
        product.UpdatedAt = DateTime.UtcNow;

        AddHistory(product, action, CurrentUserId, CurrentUserName, "Capturer submitted changes for approval.");
        await _db.SaveChangesAsync();

        return Ok(ToResponse(product));
    }

    [HttpPost("{id:int}/approve")]
    [Authorize(Roles = "Manager")]
    public async Task<ActionResult<ProductResponse>> Approve(int id)
    {
        var product = await ProductsWithHistory.FirstOrDefaultAsync(p => p.Id == id);
        if (product is null)
        {
            return NotFound();
        }

        if (product.Status != ProductStatus.PendingApproval)
        {
            return BadRequest(new { message = "Only pending products can be approved." });
        }

        if (product.CreatedBy == CurrentUserId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "A manager cannot approve their own change." });
        }

        if (product.PendingDelete)
        {
            product.Status = ProductStatus.SoftDeleted;
            AddHistory(product, "SoftDeleted", CurrentUserId, CurrentUserName, "Manager approved the delete request.");

            var cacheRow = await _db.ApprovedProductsCache.FirstOrDefaultAsync(c => c.ProductId == product.Id);
            if (cacheRow is not null)
            {
                _db.ApprovedProductsCache.Remove(cacheRow);
            }
        }
        else
        {
            product.Status = ProductStatus.Approved;
            AddHistory(product, "Approved", CurrentUserId, CurrentUserName, "Manager approved the change.");

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
            cache.ApprovedBy = CurrentUserName;
        }

        product.PendingDelete = false;
        product.UpdatedBy = CurrentUserId;
        product.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(ToResponse(product));
    }

    [HttpPost("{id:int}/reject")]
    [Authorize(Roles = "Manager")]
    public async Task<ActionResult<ProductResponse>> Reject(int id, [FromBody] RejectRequest? request)
    {
        var product = await ProductsWithHistory.FirstOrDefaultAsync(p => p.Id == id);
        if (product is null)
        {
            return NotFound();
        }

        if (product.Status != ProductStatus.PendingApproval)
        {
            return BadRequest(new { message = "Only pending products can be rejected." });
        }

        product.Status = ProductStatus.Draft;
        product.PendingDelete = false;
        product.UpdatedBy = CurrentUserId;
        product.UpdatedAt = DateTime.UtcNow;

        AddHistory(product, "Rejected", CurrentUserId, CurrentUserName, request?.Reason);
        await _db.SaveChangesAsync();

        return Ok(ToResponse(product));
    }

    public record RejectRequest(string? Reason);

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Manager")]
    public async Task<ActionResult<ProductResponse>> Delete(int id)
    {
        var product = await ProductsWithHistory.FirstOrDefaultAsync(p => p.Id == id);
        if (product is null)
        {
            return NotFound();
        }

        if (product.Status == ProductStatus.SoftDeleted || product.PendingDelete)
        {
            return BadRequest(new { message = "Product is already deleted or pending deletion." });
        }

        product.Status = ProductStatus.PendingApproval;
        product.PendingDelete = true;
        product.UpdatedBy = CurrentUserId;
        product.UpdatedAt = DateTime.UtcNow;

        AddHistory(product, "SoftDeleteRequested", CurrentUserId, CurrentUserName, "Manager requested a soft delete.");
        await _db.SaveChangesAsync();

        return Ok(ToResponse(product));
    }

    [HttpGet("approved")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<ApprovedResponse>>> GetApproved()
    {
        var rows = await _db.ApprovedProductsCache
            .OrderByDescending(c => c.ApprovedAt)
            .Select(c => new ApprovedResponse(
                c.Id,
                c.ProductId,
                c.Name,
                c.Description,
                c.Price,
                c.Stock,
                c.ApprovedAt,
                c.ApprovedBy))
            .ToListAsync();

        return Ok(rows);
    }

    private void AddHistory(Product product, string action, string actorId, string actorName, string? note)
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
