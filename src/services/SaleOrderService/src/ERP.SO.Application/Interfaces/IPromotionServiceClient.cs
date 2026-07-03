namespace ERP.SO.Application.Interfaces
{
    /// <summary>
    /// Synchronous read-only call to PromotionService to validate a coupon at order
    /// creation time — allowed under CLAUDE.md's integration rules (cross-service REST
    /// reads are fine; only cross-database access and duplicating another service's
    /// data as a system-of-record are not). Not an event because the caller needs the
    /// discount amount back in the same request/response cycle to build the order.
    /// </summary>
    public interface IPromotionServiceClient
    {
        Task<CouponValidationResult> ValidateCouponAsync(string code, decimal orderAmount, CancellationToken ct);
    }

    public class CouponValidationResult
    {
        public bool IsValid { get; set; }
        public decimal DiscountAmount { get; set; }
        public Guid? PromotionId { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
