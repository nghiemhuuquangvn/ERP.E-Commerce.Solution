using ERP.SO.Application.DTOs;
using ERP.SO.Domain.Entities;

namespace ERP.SO.Application.Handlers
{
    /// <summary>
    /// Shared SalesOrder -> SalesOrderDto mapper for the new (2026-07-03) handlers —
    /// the pre-existing handlers each keep their own private copy of this mapping;
    /// not refactored here to avoid touching working, unrelated code.
    /// </summary>
    public static class SalesOrderMapper
    {
        public static SalesOrderDto MapToDto(SalesOrder order) => new()
        {
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            CustomerName = order.CustomerName,
            CustomerEmail = order.CustomerEmail,
            Status = order.Status.ToString(),
            TotalAmount = order.TotalAmount,
            OrderDate = order.OrderDate,
            ApprovedAt = order.ApprovedAt,
            ShippedAt = order.ShippedAt,
            DeliveredAt = order.DeliveredAt,
            CancelledAt = order.CancelledAt,
            TrackingNumber = order.TrackingNumber,
            Carrier = order.Carrier,
            Notes = order.Notes,
            HasPriceChangeWarning = order.HasPriceChangeWarning,
            ReturnRequestedAt = order.ReturnRequestedAt,
            ReturnedAt = order.ReturnedAt,
            ReturnReason = order.ReturnReason,
            RefundAmount = order.RefundAmount,
            OrderNo = order.OrderNo,
            OrderSource = order.OrderSource.ToString(),
            DepartmentId = order.DepartmentId,
            SalesRepId = order.SalesRepId,
            QuotationId = order.QuotationId,
            PromotionId = order.PromotionId,
            DiscountAmount = order.DiscountAmount,
            ShippingAddress = new AddressDto
            {
                Street = order.ShippingAddress.Street,
                City = order.ShippingAddress.City,
                State = order.ShippingAddress.State,
                PostalCode = order.ShippingAddress.PostalCode,
                Country = order.ShippingAddress.Country
            },
            LineItems = order.LineItems.Select(li => new OrderLineItemDto
            {
                ProductId = li.ProductId,
                ProductName = li.ProductName,
                Quantity = li.Quantity,
                UnitPrice = li.UnitPrice,
                ShippedQuantity = li.ShippedQuantity
            }).ToList(),
            InternalNotes = order.InternalNotes.Select(n => new OrderNoteDto
            {
                Note = n.Note,
                AddedBy = n.AddedBy,
                CreatedAt = n.CreatedAt
            }).ToList(),
            StatusHistory = order.StatusHistory.Select(h => new OrderStatusHistoryDto
            {
                FromStatus = h.FromStatus.ToString(),
                ToStatus = h.ToStatus.ToString(),
                Reason = h.Reason,
                ChangedBy = h.ChangedBy,
                ChangedAt = h.ChangedAt
            }).ToList()
        };
    }
}
