using ERP.SO.Application.DTOs;
using MediatR;

namespace ERP.SO.Application.Queries
{
    public class GetQuotationQuery : IRequest<SalesQuotationDto>
    {
        public Guid QuotationId { get; set; }

        public GetQuotationQuery(Guid quotationId)
        {
            QuotationId = quotationId;
        }
    }

    public class ListQuotationsQuery : IRequest<ListQuotationsResponse>
    {
        public Guid? CustomerId { get; set; }
        public int Skip { get; set; }
        public int Take { get; set; } = 20;
    }

    public class ListQuotationsResponse
    {
        public int Total { get; set; }
        public List<SalesQuotationSummaryDto> Items { get; set; } = new();
    }
}
