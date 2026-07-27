using ERP.SO.Application.DTOs;
using ERP.SO.Application.Interfaces;
using ERP.SO.Application.Queries;
using MediatR;

namespace ERP.SO.Application.Handlers
{
    public class GetQuotationQueryHandler : IRequestHandler<GetQuotationQuery, SalesQuotationDto>
    {
        private readonly ISalesQuotationRepository _repository;

        public GetQuotationQueryHandler(ISalesQuotationRepository repository) => _repository = repository;

        public async Task<SalesQuotationDto> Handle(GetQuotationQuery request, CancellationToken cancellationToken)
        {
            var quotation = await _repository.GetByIdAsync(request.QuotationId, cancellationToken)
                ?? throw new InvalidOperationException($"Quotation {request.QuotationId} not found");

            return SalesQuotationMapper.MapToDto(quotation);
        }
    }

    public class ListQuotationsQueryHandler : IRequestHandler<ListQuotationsQuery, ListQuotationsResponse>
    {
        private readonly ISalesQuotationRepository _repository;

        public ListQuotationsQueryHandler(ISalesQuotationRepository repository) => _repository = repository;

        public async Task<ListQuotationsResponse> Handle(ListQuotationsQuery request, CancellationToken cancellationToken)
        {
            var total = await _repository.GetTotalCountAsync(request.CustomerId, cancellationToken);
            var quotations = await _repository.GetAllAsync(request.Skip, request.Take, cancellationToken);

            if (request.CustomerId.HasValue)
                quotations = quotations.Where(q => q.CustomerId == request.CustomerId).ToList();

            return new ListQuotationsResponse
            {
                Total = total,
                Items = quotations.Select(q => new SalesQuotationSummaryDto
                {
                    QuotationId = q.Id,
                    CustomerId = q.CustomerId,
                    CustomerName = q.CustomerName,
                    QuotationNo = q.QuotationNo,
                    Status = q.Status.ToString(),
                    TotalAmount = q.TotalAmount,
                    ValidUntil = q.ValidUntil
                }).ToList()
            };
        }
    }
}
