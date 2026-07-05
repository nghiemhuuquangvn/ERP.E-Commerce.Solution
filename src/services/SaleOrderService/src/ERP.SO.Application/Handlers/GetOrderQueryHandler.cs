using ERP.Shared.Infrastructure.Caching;
using ERP.SO.Application.DTOs;
using ERP.SO.Application.Queries;
using ERP.SO.Domain.Entities;
using MediatR;

namespace ERP.SO.Application.Handlers
{
    public class GetOrderQueryHandler : IRequestHandler<GetOrderQuery, SalesOrderDto>
    {
        private readonly ISalesOrderRepository _repository;
        private readonly ICacheService _cache;

        public GetOrderQueryHandler(ISalesOrderRepository repository, ICacheService cache)
        {
            _repository = repository;
            _cache = cache;
        }

        public async Task<SalesOrderDto> Handle(GetOrderQuery request, CancellationToken cancellationToken)
        {
            var cacheKey = $"so:order:{request.OrderId}";
            var cached = await _cache.GetAsync<SalesOrderDto>(cacheKey, cancellationToken);
            if (cached != null) return cached;

            var order = await _repository.GetByIdAsync(request.OrderId, cancellationToken)
                ?? throw new InvalidOperationException($"Order {request.OrderId} not found");

            var dto = SalesOrderMapper.MapToDto(order);
            await _cache.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(5), cancellationToken);
            return dto;
        }
    }

    public class ListOrdersQueryHandler : IRequestHandler<ListOrdersQuery, ListOrdersResponse>
    {
        private readonly ISalesOrderRepository _repository;

        public ListOrdersQueryHandler(ISalesOrderRepository repository)
        {
            _repository = repository;
        }

        public async Task<ListOrdersResponse> Handle(ListOrdersQuery request, CancellationToken cancellationToken)
        {
            var status = string.IsNullOrEmpty(request.Status) ? (OrderStatus?)null : Enum.Parse<OrderStatus>(request.Status);
            var total = await _repository.GetTotalCountAsync(request.CustomerId, status, cancellationToken);

            var orders = await _repository.GetAllAsync(request.Skip, request.Take, cancellationToken);

            if (request.CustomerId.HasValue)
                orders = orders.Where(o => o.CustomerId == request.CustomerId).ToList();

            if (status.HasValue)
                orders = orders.Where(o => o.Status == status).ToList();

            return new ListOrdersResponse
            {
                Total = total,
                Items = orders
                    .Select(o => new SalesOrderSummaryDto
                    {
                        OrderId = o.Id,
                        CustomerId = o.CustomerId,
                        CustomerName = o.CustomerName,
                        Status = o.Status.ToString(),
                        TotalAmount = o.TotalAmount,
                        OrderDate = o.OrderDate
                    })
                    .ToList()
            };
        }
    }

    public class GetOrdersByCustomerQueryHandler : IRequestHandler<GetOrdersByCustomerQuery, List<SalesOrderSummaryDto>>
    {
        private readonly ISalesOrderRepository _repository;
        private readonly ICacheService _cache;

        public GetOrdersByCustomerQueryHandler(ISalesOrderRepository repository, ICacheService cache)
        {
            _repository = repository;
            _cache = cache;
        }

        public async Task<List<SalesOrderSummaryDto>> Handle(
            GetOrdersByCustomerQuery request, CancellationToken cancellationToken)
        {
            var cacheKey = $"so:customer:{request.CustomerId}:orders";
            var cached = await _cache.GetAsync<List<SalesOrderSummaryDto>>(cacheKey, cancellationToken);
            if (cached != null) return cached;

            var orders = await _repository.GetByCustomerIdAsync(request.CustomerId, cancellationToken);

            var result = orders
                .Select(o => new SalesOrderSummaryDto
                {
                    OrderId = o.Id,
                    CustomerId = o.CustomerId,
                    CustomerName = o.CustomerName,
                    Status = o.Status.ToString(),
                    TotalAmount = o.TotalAmount,
                    OrderDate = o.OrderDate
                })
                .ToList();

            await _cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(2), cancellationToken);
            return result;
        }
    }
}
