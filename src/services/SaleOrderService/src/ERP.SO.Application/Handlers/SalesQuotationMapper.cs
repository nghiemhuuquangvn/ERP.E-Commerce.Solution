using ERP.SO.Application.DTOs;
using ERP.SO.Domain.Entities;

namespace ERP.SO.Application.Handlers
{
    public static class SalesQuotationMapper
    {
        public static SalesQuotationDto MapToDto(SalesQuotation quotation) => new()
        {
            QuotationId = quotation.Id,
            CustomerId = quotation.CustomerId,
            CustomerName = quotation.CustomerName,
            CustomerEmail = quotation.CustomerEmail,
            QuotationNo = quotation.QuotationNo,
            Status = quotation.Status.ToString(),
            ValidUntil = quotation.ValidUntil,
            TotalAmount = quotation.TotalAmount,
            ConvertedOrderId = quotation.ConvertedOrderId,
            LineItems = quotation.LineItems.Select(li => new QuotationLineItemDto
            {
                ProductId = li.ProductId,
                ProductName = li.ProductName,
                Quantity = li.Quantity,
                UnitPrice = li.UnitPrice
            }).ToList()
        };
    }
}
