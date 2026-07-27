using ERP.SO.Application.DTOs;
using MediatR;

namespace ERP.SO.Application.Commands
{
    public class CreateQuotationCommand : IRequest<SalesQuotationDto>
    {
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public List<QuotationLineItemDto> LineItems { get; set; } = new();
        public DateTime ValidUntil { get; set; }

        public CreateQuotationCommand() { }

        public CreateQuotationCommand(CreateQuotationRequest request)
        {
            CustomerId = request.CustomerId;
            CustomerName = request.CustomerName;
            CustomerEmail = request.CustomerEmail;
            LineItems = request.LineItems;
            ValidUntil = request.ValidUntil;
        }
    }

    public class SendQuotationCommand : IRequest<SalesQuotationDto>
    {
        public Guid QuotationId { get; set; }
    }

    public class AcceptQuotationCommand : IRequest<SalesQuotationDto>
    {
        public Guid QuotationId { get; set; }
    }

    public class RejectQuotationCommand : IRequest<SalesQuotationDto>
    {
        public Guid QuotationId { get; set; }
    }

    /// <summary>Converts an Accepted quotation into a real SalesOrder — pricing is carried over as-is (the quotation's own snapshot); re-pricing against a since-changed PriceList is a follow-up, not built in this round.</summary>
    public class ConvertQuotationToOrderCommand : IRequest<SalesOrderDto>
    {
        public Guid QuotationId { get; set; }
        public AddressDto ShippingAddress { get; set; } = new();
    }
}
