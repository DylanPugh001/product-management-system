namespace ProductManagementSystem.Api.Models;

public class ProductApprovalHistory
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? Note { get; set; }

    public Product? Product { get; set; }
}
