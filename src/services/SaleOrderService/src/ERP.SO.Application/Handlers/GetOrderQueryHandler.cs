using ERP.SO.Application.DTOs;
using ERP.SO.Application.Queries;
using ERP.SO.Domain.Entities;
using MediatR;

namespace ERP.SO.Application.Handlers
{
    public class GetOrderQueryHandler : IRequestHandler<GetOrderQuery, SalesOrderDto>
    {
        private readonly ISalesOrderRepository _repository;

        public GetOrderQueryHandler(ISalesOrderRepository repository)
        {
            _repository = repository;
        }

        public async Task<SalesOrderDto> Handle(GetOrderQuery request, CancellationToken cancellationToken)
        {
            var order = await _repository.GetByIdAsync(request.OrderId, cancellationToken)
                ?? throw new InvalidOperationException($"Order {request.OrderId} not found");

            return MapToDto(order);
        }

        private SalesOrderDto MapToDto(SalesOrder order)
        {
            return new SalesOrderDto
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
                Notes = order.Notes,
                ShippingAddress = new AddressDto
                {
                    Street = order.ShippingAddress.Street,
                    City = order.ShippingAddress.City,
                    State = order.ShippingAddress.State,
                    PostalCode = order.ShippingAddress.PostalCode,
                    Country = order.ShippingAddress.Country
                },
                LineItems = order.LineItems
                    .Select(li => new OrderLineItemDto
                    {
                        ProductId = li.ProductId,
                        ProductName = li.ProductName,
                        Quantity = li.Quantity,
                        UnitPrice = li.UnitPrice
                    })
                    .ToList()
            };
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
            {
                orders = orders.Where(o => o.CustomerId == request.CustomerId).ToList();
            }

            if (status.HasValue)
            {
                orders = orders.Where(o => o.Status == status).ToList();
            }

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

        public GetOrdersByCustomerQueryHandler(ISalesOrderRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<SalesOrderSummaryDto>> Handle(
            GetOrdersByCustomerQuery request, CancellationToken cancellationToken)
        {
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
