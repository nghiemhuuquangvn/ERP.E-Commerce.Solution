using ERP.Shared.Infrastructure.Exceptions;
using ERP.SO.Domain.Entities;

namespace ERP.SO.Application.Queries
{
    /// <summary>
    /// What the sales order list can be narrowed by (Admin web and mobile app). Applied inside the database query, before paging,
    /// so the total counts every match. <c>From</c> / <c>To</c> are calendar days of <c>OrderDate</c>, both inclusive.
    /// </summary>
    public sealed record OrderListFilter(Guid? CustomerId = null, OrderStatus? Status = null, DateOnly? From = null, DateOnly? To = null, string? Search = null)
    {
        public IQueryable<SalesOrder> Apply(IQueryable<SalesOrder> orders)
        {
            if (From.HasValue && To.HasValue && From > To)
                throw new BusinessRuleException("Sales", "The start date is after the end date.");

            if (CustomerId.HasValue) orders = orders.Where(o => o.CustomerId == CustomerId.Value);
            if (Status.HasValue) orders = orders.Where(o => o.Status == Status.Value);
            if (From.HasValue)
            {
                var from = From.Value.ToDateTime(TimeOnly.MinValue);
                orders = orders.Where(o => o.OrderDate >= from);
            }
            if (To.HasValue)
            {
                var before = To.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);
                orders = orders.Where(o => o.OrderDate < before);
            }
            var text = Search?.Trim().ToLower();
            if (!string.IsNullOrEmpty(text))
                orders = orders.Where(o => o.OrderNo.ToLower().Contains(text) || o.CustomerName.ToLower().Contains(text));
            return orders;
        }

        /// <summary>An unknown status is refused (400); it used to throw a 500.</summary>
        public static OrderStatus? ParseStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status)) return null;
            if (Enum.TryParse<OrderStatus>(status.Replace("_", "").Replace(" ", ""), true, out var parsed) && Enum.IsDefined(parsed)) return parsed;
            throw new BusinessRuleException("Sales", $"Unknown order status '{status}'.");
        }
    }
}
