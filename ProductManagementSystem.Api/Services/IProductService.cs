using ProductManagementSystem.Api.Models;

namespace ProductManagementSystem.Api.Services;

public record ProductRequest(string Name, string? Description, decimal Price, int Stock);

public interface IProductService
{
    Task<IReadOnlyList<Product>> GetAllAsync(string userId, bool isManager, int page = 1, int pageSize = 50);
    Task<Product?> GetByIdAsync(int id);
    Task<Product> CreateAsync(ProductRequest request, string actorId, string actorName);
    Task<Product?> UpdateAsync(int id, ProductRequest request, string actorId, string actorName);
    Task<Product?> ApproveAsync(int id, string actorId, string actorName);
    Task<Product?> RejectAsync(int id, string? reason, string actorId, string actorName);
    Task<Product?> RequestDeleteAsync(int id, string actorId, string actorName);
    Task<IReadOnlyList<ApprovedProductsCache>> GetApprovedAsync(int page = 1, int pageSize = 50);
}
