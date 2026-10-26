using ERP.Shared.Core.Domain;

namespace ERP.NTF.Domain.Entities;

/// <summary>
/// Who an order's notifications go to (ROADMAP P1-6). Written from the Sales <c>SalesOrderCreated</c> event, which carries the
/// customer's e-mail; payment, shipping and order-status events only know the order id, so their handlers look it up here.
/// A local projection (like HRM's EmployeeSnapshot): no call to Sales.
/// </summary>
public class OrderRecipient : BaseEntity
{
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
}

/// <summary>An account's e-mail, from the <c>UserCreated</c> event; used for notifications that only know the user id (loyalty points, coupons).</summary>
public class UserContact : BaseEntity
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
}
