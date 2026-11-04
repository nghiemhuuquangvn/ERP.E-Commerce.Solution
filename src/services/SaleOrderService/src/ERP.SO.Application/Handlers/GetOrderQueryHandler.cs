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
            // Not cached (ROADMAP P1-11d): the order changes through events as well (delivered, goods received for a return) and nothing evicted the old
            // 5-minute copy, so a screen showed a stale status; the key did not carry the tenant either.
            var order = await _repository.GetByIdAsync(request.OrderId, cancellationToken)
                ?? throw new InvalidOperationException($"Order {request.OrderId} not found");

            var dto = SalesOrderMapper.MapToDto(order);

            var customerOrders = await _repository.GetByCustomerIdAsync(order.CustomerId, cancellationToken);
            var (isAnomalous, reason) = OrderAnomalyDetector.Evaluate(order.Id, order.TotalAmount, customerOrders);
            dto.IsAnomalous = isAnomalous;
            dto.AnomalyReason = reason;

            return dto;
        }
    }

    /// <summary>
    /// Rule-based (not ML) sales-order anomaly check: flags an order whose total
    /// is far above what this customer normally spends, using the customer's own
    /// OTHER orders as the baseline. Deliberately simple/explainable — a
    /// heuristic for staff review, not a statistical model.
    /// </summary>
    public static class OrderAnomalyDetector
    {
        private const int MinHistoryCount = 3;
        private const decimal ThresholdMultiplier = 3.0m;

        public static (bool IsAnomalous, string? Reason) Evaluate(
            Guid orderId, decimal orderTotal, IReadOnlyCollection<SalesOrder> customerOrders)
        {
            var history = customerOrders.Where(o => o.Id != orderId).ToList();
            if (history.Count < MinHistoryCount)
                return (false, null);

            var average = history.Average(o => o.TotalAmount);
            if (average <= 0 || orderTotal <= average * ThresholdMultiplier)
                return (false, null);

            var multiple = average > 0 ? orderTotal / average : 0;
            return (true, $"Order total is {multiple:0.#}x this customer's average order (${average:N2} over {history.Count} prior orders)");
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

            // The filters belong in the query: filtering a page afterwards showed a page of "all" orders minus the ones that did not
            // match, while the total counted every match (a status filter on page 1 could be empty with hundreds of matches).
            var orders = await _repository.GetAllAsync(request.Skip, request.Take, request.CustomerId, status, cancellationToken);

            // One GetByCustomerIdAsync per distinct customer on this page (not per
            // order) — a page is at most `Take` orders, so this is bounded and cheap
            // for an admin list view, not a hot path.
            var historyByCustomer = new Dictionary<Guid, List<SalesOrder>>();
            foreach (var customerId in orders.Select(o => o.CustomerId).Distinct())
                historyByCustomer[customerId] = await _repository.GetByCustomerIdAsync(customerId, cancellationToken);

            return new ListOrdersResponse
            {
                Total = total,
                Items = orders
                    .Select(o =>
                    {
                        var (isAnomalous, reason) = OrderAnomalyDetector.Evaluate(o.Id, o.TotalAmount, historyByCustomer[o.CustomerId]);
                        return new SalesOrderSummaryDto
                        {
                            OrderId = o.Id,
                            CustomerId = o.CustomerId,
                            CustomerName = o.CustomerName,
                            Status = o.Status.ToString(),
                            TotalAmount = o.TotalAmount,
                            OrderDate = o.OrderDate,
                            OrderNo = o.OrderNo,
                            SalesRepId = o.SalesRepId,
                            DepartmentId = o.DepartmentId,
                            IsAnomalous = isAnomalous,
                            AnomalyReason = reason
                        };
                    })
                    .ToList()
            };
        }
    }

    public class GetOrdersByCustomerQueryHandler : IRequestHandler<GetOrdersByCustomerQuery, List<SalesOrderSummaryDto>>
    {
        private readonly ISalesOrderRepository _repository;

        public GetOrdersByCustomerQueryHandler(ISalesOrderRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<SalesOrderSummaryDto>> Handle(
            GetOrdersByCustomerQuery request, CancellationToken cancellationToken)
        {
            // Not cached (R97): the old key 'so:customer:{id}:orders' had no tenant and nothing evicted it, so a customer who had just ordered
            // did not see the order in their list for 2 minutes.
            var orders = await _repository.GetByCustomerIdAsync(request.CustomerId, cancellationToken);

            return orders
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
        }
    }
}
