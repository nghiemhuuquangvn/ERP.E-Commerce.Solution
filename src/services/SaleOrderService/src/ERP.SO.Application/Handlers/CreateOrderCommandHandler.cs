using ERP.Shared.Messaging;
using ERP.SO.Application.Commands;
using ERP.SO.Application.DTOs;
using ERP.SO.Application.Interfaces;
using ERP.SO.Domain.Entities;
using ERP.SO.Domain.ValueObjects;
using MediatR;

namespace ERP.SO.Application.Handlers
{
    public interface ISalesOrderRepository
    {
        Task<SalesOrder?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task AddAsync(SalesOrder order, CancellationToken ct = default);
        Task UpdateAsync(SalesOrder order, CancellationToken ct = default);
        Task<List<SalesOrder>> GetByCustomerIdAsync(Guid customerId, CancellationToken ct = default);
        Task<List<SalesOrder>> GetAllAsync(int skip = 0, int take = 20, CancellationToken ct = default);
        Task<int> GetTotalCountAsync(Guid? customerId = null, OrderStatus? status = null, CancellationToken ct = default);
        Task<List<SalesOrder>> GetByStatusAsync(OrderStatus status, CancellationToken ct = default);
        Task<List<SalesOrder>> GetOpenOrdersContainingProductAsync(Guid productId, CancellationToken ct = default);
    }

    public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, SalesOrderDto>
    {
        private readonly ISalesOrderUnitOfWork _uow;
        private readonly IMessageBus _messageBus;
        private readonly IPromotionServiceClient _promotionServiceClient;

        public CreateOrderCommandHandler(ISalesOrderUnitOfWork uow, IMessageBus messageBus, IPromotionServiceClient promotionServiceClient)
        {
            _uow = uow;
            _messageBus = messageBus;
            _promotionServiceClient = promotionServiceClient;
        }

        public async Task<SalesOrderDto> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
        {
            var address = new Address(
                request.ShippingAddress.Street,
                request.ShippingAddress.City,
                request.ShippingAddress.State,
                request.ShippingAddress.PostalCode,
                request.ShippingAddress.Country);

            var lineItems = request.LineItems
                .Select(li => new OrderLineItem(li.ProductId, li.ProductName, li.Quantity, li.UnitPrice))
                .ToList();

            var salesOrder = SalesOrder.Create(
                request.CustomerId,
                request.CustomerName,
                request.CustomerEmail,
                address,
                lineItems,
                request.Notes);

            if (!string.IsNullOrWhiteSpace(request.CouponCode))
            {
                var validation = await _promotionServiceClient.ValidateCouponAsync(request.CouponCode, salesOrder.TotalAmount, cancellationToken);
                if (validation.IsValid)
                    salesOrder.ApplyDiscount(validation.DiscountAmount);
                else
                    salesOrder.AddInternalNote($"Coupon '{request.CouponCode}' not applied: {validation.ErrorMessage}", "system:promotion-validation");
            }

            await _uow.Orders.AddAsync(salesOrder, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);

            foreach (var domainEvent in salesOrder.DomainEvents)
            {
                await _messageBus.PublishAsync(domainEvent, cancellationToken);
            }

            return MapToDto(salesOrder);
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

    public class ApproveOrderCommandHandler : IRequestHandler<ApproveOrderCommand, SalesOrderDto>
    {
        private readonly ISalesOrderUnitOfWork _uow;
        private readonly IMessageBus _messageBus;

        public ApproveOrderCommandHandler(ISalesOrderUnitOfWork uow, IMessageBus messageBus)
        {
            _uow = uow;
            _messageBus = messageBus;
        }

        public async Task<SalesOrderDto> Handle(ApproveOrderCommand request, CancellationToken cancellationToken)
        {
            var order = await _uow.Orders.GetByIdAsync(request.OrderId, cancellationToken)
                ?? throw new InvalidOperationException($"Order {request.OrderId} not found");

            order.Approve();
            await _uow.Orders.UpdateAsync(order, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);

            foreach (var domainEvent in order.DomainEvents)
            {
                await _messageBus.PublishAsync(domainEvent, cancellationToken);
            }

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

    public class ShipOrderCommandHandler : IRequestHandler<ShipOrderCommand, SalesOrderDto>
    {
        private readonly ISalesOrderUnitOfWork _uow;
        private readonly IMessageBus _messageBus;

        public ShipOrderCommandHandler(ISalesOrderUnitOfWork uow, IMessageBus messageBus)
        {
            _uow = uow;
            _messageBus = messageBus;
        }

        public async Task<SalesOrderDto> Handle(ShipOrderCommand request, CancellationToken cancellationToken)
        {
            var order = await _uow.Orders.GetByIdAsync(request.OrderId, cancellationToken)
                ?? throw new InvalidOperationException($"Order {request.OrderId} not found");

            order.Ship(request.TrackingNumber, request.Carrier);
            await _uow.Orders.UpdateAsync(order, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);

            foreach (var domainEvent in order.DomainEvents)
            {
                await _messageBus.PublishAsync(domainEvent, cancellationToken);
            }

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

    public class CancelOrderCommandHandler : IRequestHandler<CancelOrderCommand, SalesOrderDto>
    {
        private readonly ISalesOrderUnitOfWork _uow;
        private readonly IMessageBus _messageBus;

        public CancelOrderCommandHandler(ISalesOrderUnitOfWork uow, IMessageBus messageBus)
        {
            _uow = uow;
            _messageBus = messageBus;
        }

        public async Task<SalesOrderDto> Handle(CancelOrderCommand request, CancellationToken cancellationToken)
        {
            var order = await _uow.Orders.GetByIdAsync(request.OrderId, cancellationToken)
                ?? throw new InvalidOperationException($"Order {request.OrderId} not found");

            order.Cancel(request.Reason);
            await _uow.Orders.UpdateAsync(order, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);

            foreach (var domainEvent in order.DomainEvents)
            {
                await _messageBus.PublishAsync(domainEvent, cancellationToken);
            }

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
}
