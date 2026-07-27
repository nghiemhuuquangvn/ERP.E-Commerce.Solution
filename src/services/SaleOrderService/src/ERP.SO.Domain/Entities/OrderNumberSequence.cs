namespace ERP.SO.Domain.Entities
{
    /// <summary>Per-tenant, per-month counter backing OrderNo/QuotationNo generation — incremented atomically via a MERGE...OUTPUT statement, not through the EF change tracker.</summary>
    public class OrderNumberSequence
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Prefix { get; set; } = string.Empty;
        public int Year { get; set; }
        public int Month { get; set; }
        public int LastValue { get; set; }
    }
}
