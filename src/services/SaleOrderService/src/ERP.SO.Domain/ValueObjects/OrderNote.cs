namespace ERP.SO.Domain.ValueObjects
{
    /// <summary>
    /// One internal comment on an order — distinct from SalesOrder.Notes (the single
    /// customer-facing note captured at creation time). Multiple of these can
    /// accumulate over the order's lifecycle, each timestamped and attributed.
    /// </summary>
    public class OrderNote
    {
        public string Note { get; set; } = string.Empty;
        public string? AddedBy { get; set; }
        public DateTime CreatedAt { get; set; }

        public OrderNote() { }

        public OrderNote(string note, string? addedBy)
        {
            Note = note;
            AddedBy = addedBy;
            CreatedAt = DateTime.UtcNow;
        }
    }
}
