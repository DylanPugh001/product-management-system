using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProductManagementSystem.Api.Data;
using ProductManagementSystem.Api.Models;
using ProductManagementSystem.Api.Services;

namespace ProductManagementSystem.Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

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

    public record RejectRequest(string? Reason);

    private ProductResponse ToResponse(Product product, bool includeHistory = true) => new(
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
        includeHistory
            ? product.History
                .OrderByDescending(h => h.Timestamp)
                .Select(h => new HistoryResponse(h.Id, h.Action, h.ActorName, h.Timestamp, h.Note))
                .ToList()
            : new List<HistoryResponse>());

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    private string CurrentUserName =>
        User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue(ClaimTypes.Name) ?? CurrentUserId;

    private bool IsManager => User.IsInRole(DbInitializer.Roles.Manager);

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<IEnumerable<ProductResponse>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var products = await _productService.GetAllAsync(CurrentUserId, IsManager, page, pageSize);
        return Ok(products.Select(p => ToResponse(p, includeHistory: false)));
    }

    [HttpGet("{id:int}")]
    [Authorize]
    public async Task<ActionResult<ProductResponse>> GetById(int id)
    {
        var product = await _productService.GetByIdAsync(id);
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
    [Authorize(Roles = DbInitializer.Roles.Capturer)]
    public async Task<ActionResult<ProductResponse>> Create(ProductRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Name is required." });
        }

        if (request.Description?.Length > 2000)
            return BadRequest(new { message = "Description must be 2000 characters or fewer." });

        if (request.Price < 0)
            return BadRequest(new { message = "Price must be non-negative." });

        if (request.Stock < 0)
            return BadRequest(new { message = "Stock must be non-negative." });

        try
        {
            var product = await _productService.CreateAsync(request, CurrentUserId, CurrentUserName);
            return CreatedAtAction(nameof(GetById), new { id = product.Id }, ToResponse(product));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = DbInitializer.Roles.Capturer)]
    public async Task<ActionResult<ProductResponse>> Update(int id, ProductRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Name is required." });
        }

        if (request.Description?.Length > 2000)
            return BadRequest(new { message = "Description must be 2000 characters or fewer." });

        if (request.Price < 0)
            return BadRequest(new { message = "Price must be non-negative." });

        if (request.Stock < 0)
            return BadRequest(new { message = "Stock must be non-negative." });

        try
        {
            var product = await _productService.UpdateAsync(id, request, CurrentUserId, CurrentUserName);
            if (product is null)
            {
                return NotFound();
            }

            return Ok(ToResponse(product));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/approve")]
    [Authorize(Roles = DbInitializer.Roles.Manager)]
    public async Task<ActionResult<ProductResponse>> Approve(int id)
    {
        try
        {
            var product = await _productService.ApproveAsync(id, CurrentUserId, CurrentUserName);
            if (product is null)
            {
                return NotFound();
            }

            return Ok(ToResponse(product));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/reject")]
    [Authorize(Roles = DbInitializer.Roles.Manager)]
    public async Task<ActionResult<ProductResponse>> Reject(int id, [FromBody] RejectRequest? request)
    {
        if (request?.Reason?.Length > 500)
            return BadRequest(new { message = "Reason must be 500 characters or fewer." });

        try
        {
            var product = await _productService.RejectAsync(id, request?.Reason, CurrentUserId, CurrentUserName);
            if (product is null)
            {
                return NotFound();
            }

            return Ok(ToResponse(product));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = DbInitializer.Roles.Manager)]
    public async Task<ActionResult<ProductResponse>> Delete(int id)
    {
        try
        {
            var product = await _productService.RequestDeleteAsync(id, CurrentUserId, CurrentUserName);
            if (product is null)
            {
                return NotFound();
            }

            return Ok(ToResponse(product));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("approved")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<ApprovedResponse>>> GetApproved(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var rows = await _productService.GetApprovedAsync(page, pageSize);
        return Ok(rows.Select(c => new ApprovedResponse(
            c.Id,
            c.ProductId,
            c.Name,
            c.Description,
            c.Price,
            c.Stock,
            c.ApprovedAt,
            c.ApprovedBy)));
    }
}
