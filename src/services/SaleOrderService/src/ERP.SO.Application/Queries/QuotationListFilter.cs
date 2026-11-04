using ERP.Shared.Infrastructure.Exceptions;
using ERP.SO.Domain.Entities;

namespace ERP.SO.Application.Queries
{
    /// <summary>
    /// What the quotation list can be narrowed by. Applied inside the database query, before paging, so the total counts every match
    /// (the customer filter used to be applied to a page afterwards - R124). <c>From</c> / <c>To</c> are calendar days of the day the
    /// quotation was made (<c>CreatedAt</c>), both inclusive.
    /// </summary>
    public sealed record QuotationListFilter(Guid? CustomerId = null, QuotationStatus? Status = null, DateOnly? From = null, DateOnly? To = null, string? Search = null)
    {
        public IQueryable<SalesQuotation> Apply(IQueryable<SalesQuotation> quotations)
        {
            if (From.HasValue && To.HasValue && From > To)
                throw new BusinessRuleException("Sales", "The start date is after the end date.");

            if (CustomerId.HasValue) quotations = quotations.Where(q => q.CustomerId == CustomerId.Value);
            if (Status.HasValue) quotations = quotations.Where(q => q.Status == Status.Value);
            if (From.HasValue)
            {
                var from = From.Value.ToDateTime(TimeOnly.MinValue);
                quotations = quotations.Where(q => q.CreatedAt >= from);
            }
            if (To.HasValue)
            {
                var before = To.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);
                quotations = quotations.Where(q => q.CreatedAt < before);
            }
            var text = Search?.Trim().ToLower();
            if (!string.IsNullOrEmpty(text))
                quotations = quotations.Where(q => q.QuotationNo.ToLower().Contains(text) || q.CustomerName.ToLower().Contains(text));
            return quotations;
        }

        /// <summary>An unknown status is refused (400).</summary>
        public static QuotationStatus? ParseStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status)) return null;
            if (Enum.TryParse<QuotationStatus>(status.Replace("_", "").Replace(" ", ""), true, out var parsed) && Enum.IsDefined(parsed)) return parsed;
            throw new BusinessRuleException("Sales", $"Unknown quotation status '{status}'.");
        }
    }
}
