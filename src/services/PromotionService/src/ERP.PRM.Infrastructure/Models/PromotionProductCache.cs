namespace ERP.PRM.Infrastructure.Models;

public class PromotionProductCache
{
    public Guid ProductId { get; set; }
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public DateTime SyncedAt { get; set; }
}
