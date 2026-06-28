using ERP.SO.Application.Commands;
using ERP.SO.Application.DTOs;
using MediatR;

namespace ERP.SO.Application.Handlers;

public class SubmitOrderCommandHandler : IRequestHandler<SubmitOrderCommand, SalesOrderDto>
{
    private readonly ISalesOrderRepository _repository;
    private readonly IMessageBus _messageBus;

    public SubmitOrderCommandHandler(ISalesOrderRepository repository, IMessageBus messageBus)
    {
        _repository = repository;
        _messageBus = messageBus;
    }

    public async Task<SalesOrderDto> Handle(SubmitOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _repository.GetByIdAsync(request.OrderId, cancellationToken)
            ?? throw new InvalidOperationException($"Order {request.OrderId} not found.");

        order.Submit();
        await _repository.UpdateAsync(order, cancellationToken);

        return MapToDto(order);
    }

    private static SalesOrderDto MapToDto(Domain.Entities.SalesOrder order) => new()
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
        LineItems = order.LineItems.Select(li => new OrderLineItemDto
        {
            ProductId = li.ProductId,
            ProductName = li.ProductName,
            Quantity = li.Quantity,
            UnitPrice = li.UnitPrice
        }).ToList()
    };
}

public class MarkAsDeliveredCommandHandler : IRequestHandler<MarkAsDeliveredCommand, SalesOrderDto>
{
    private readonly ISalesOrderRepository _repository;
    private readonly IMessageBus _messageBus;

    public MarkAsDeliveredCommandHandler(ISalesOrderRepository repository, IMessageBus messageBus)
    {
        _repository = repository;
        _messageBus = messageBus;
    }

    public async Task<SalesOrderDto> Handle(MarkAsDeliveredCommand request, CancellationToken cancellationToken)
    {
        var order = await _repository.GetByIdAsync(request.OrderId, cancellationToken)
            ?? throw new InvalidOperationException($"Order {request.OrderId} not found.");

        order.MarkAsDelivered();
        await _repository.UpdateAsync(order, cancellationToken);

        foreach (var domainEvent in order.GetDomainEvents())
            await _messageBus.PublishAsync(domainEvent, cancellationToken);

        return MapToDto(order);
    }

    private static SalesOrderDto MapToDto(Domain.Entities.SalesOrder order) => new()
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
        LineItems = order.LineItems.Select(li => new OrderLineItemDto
        {
            ProductId = li.ProductId,
            ProductName = li.ProductName,
            Quantity = li.Quantity,
            UnitPrice = li.UnitPrice
        }).ToList()
    };
}

public class RejectOrderCommandHandler : IRequestHandler<RejectOrderCommand, SalesOrderDto>
{
    private readonly ISalesOrderRepository _repository;

    public RejectOrderCommandHandler(ISalesOrderRepository repository) => _repository = repository;

    public async Task<SalesOrderDto> Handle(RejectOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _repository.GetByIdAsync(request.OrderId, cancellationToken)
            ?? throw new InvalidOperationException($"Order {request.OrderId} not found.");

        order.Reject(request.Reason);
        await _repository.UpdateAsync(order, cancellationToken);

        return MapToDto(order);
    }

    private static SalesOrderDto MapToDto(Domain.Entities.SalesOrder order) => new()
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
        LineItems = order.LineItems.Select(li => new OrderLineItemDto
        {
            ProductId = li.ProductId,
            ProductName = li.ProductName,
            Quantity = li.Quantity,
            UnitPrice = li.UnitPrice
        }).ToList()
    };
}

public class MarkAsPickedCommandHandler : IRequestHandler<MarkAsPickedCommand, SalesOrderDto>
{
    private readonly ISalesOrderRepository _repository;

    public MarkAsPickedCommandHandler(ISalesOrderRepository repository) => _repository = repository;

    public async Task<SalesOrderDto> Handle(MarkAsPickedCommand request, CancellationToken cancellationToken)
    {
        var order = await _repository.GetByIdAsync(request.OrderId, cancellationToken)
            ?? throw new InvalidOperationException($"Order {request.OrderId} not found.");

        order.MarkAsPicked();
        await _repository.UpdateAsync(order, cancellationToken);

        return MapToDto(order);
    }

    private static SalesOrderDto MapToDto(Domain.Entities.SalesOrder order) => new()
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
        LineItems = order.LineItems.Select(li => new OrderLineItemDto
        {
            ProductId = li.ProductId,
            ProductName = li.ProductName,
            Quantity = li.Quantity,
            UnitPrice = li.UnitPrice
        }).ToList()
    };
}
