using ERP.SO.Application.Commands;
using ERP.SO.Application.DTOs;
using ERP.SO.Domain.ValueObjects;
using MediatR;

namespace ERP.SO.Application.Handlers
{
    public class UpdateOrderCommandHandler : IRequestHandler<UpdateOrderCommand, SalesOrderDto>
    {
        private readonly ISalesOrderRepository _repository;

        public UpdateOrderCommandHandler(ISalesOrderRepository repository)
        {
            _repository = repository;
        }

        public async Task<SalesOrderDto> Handle(UpdateOrderCommand request, CancellationToken cancellationToken)
        {
            var order = await _repository.GetByIdAsync(request.OrderId, cancellationToken)
                ?? throw new InvalidOperationException($"Order {request.OrderId} not found");

            Address? address = request.ShippingAddress == null ? null : new Address(
                request.ShippingAddress.Street,
                request.ShippingAddress.City,
                request.ShippingAddress.State,
                request.ShippingAddress.PostalCode,
                request.ShippingAddress.Country,
                request.ShippingAddress.Ward);

            order.UpdateOrder(address, request.Notes);
            await _repository.UpdateAsync(order, cancellationToken);

            return SalesOrderMapper.MapToDto(order);
        }
    }

    public class AddOrderNoteCommandHandler : IRequestHandler<AddOrderNoteCommand, Unit>
    {
        private readonly ISalesOrderRepository _repository;

        public AddOrderNoteCommandHandler(ISalesOrderRepository repository)
        {
            _repository = repository;
        }

        public async Task<Unit> Handle(AddOrderNoteCommand request, CancellationToken cancellationToken)
        {
            var order = await _repository.GetByIdAsync(request.OrderId, cancellationToken)
                ?? throw new InvalidOperationException($"Order {request.OrderId} not found");

            order.AddInternalNote(request.Note, request.AddedBy);
            await _repository.UpdateAsync(order, cancellationToken);

            return Unit.Value;
        }
    }

    public class ShipLineItemsCommandHandler : IRequestHandler<ShipLineItemsCommand, SalesOrderDto>
    {
        private readonly ISalesOrderRepository _repository;

        public ShipLineItemsCommandHandler(ISalesOrderRepository repository)
        {
            _repository = repository;
        }

        public async Task<SalesOrderDto> Handle(ShipLineItemsCommand request, CancellationToken cancellationToken)
        {
            var order = await _repository.GetByIdAsync(request.OrderId, cancellationToken)
                ?? throw new InvalidOperationException($"Order {request.OrderId} not found");

            order.ShipLineItems(request.Quantities, request.TrackingNumber, request.Carrier);
            await _repository.UpdateAsync(order, cancellationToken);

            return SalesOrderMapper.MapToDto(order);
        }
    }

    public class RequestReturnCommandHandler : IRequestHandler<RequestReturnCommand, SalesOrderDto>
    {
        private readonly ISalesOrderRepository _repository;

        public RequestReturnCommandHandler(ISalesOrderRepository repository)
        {
            _repository = repository;
        }

        public async Task<SalesOrderDto> Handle(RequestReturnCommand request, CancellationToken cancellationToken)
        {
            var order = await _repository.GetByIdAsync(request.OrderId, cancellationToken)
                ?? throw new InvalidOperationException($"Order {request.OrderId} not found");

            order.RequestReturn(request.Reason);
            await _repository.UpdateAsync(order, cancellationToken);

            return SalesOrderMapper.MapToDto(order);
        }
    }

    public class CompleteReturnCommandHandler : IRequestHandler<CompleteReturnCommand, SalesOrderDto>
    {
        private readonly ISalesOrderRepository _repository;

        public CompleteReturnCommandHandler(ISalesOrderRepository repository)
        {
            _repository = repository;
        }

        public async Task<SalesOrderDto> Handle(CompleteReturnCommand request, CancellationToken cancellationToken)
        {
            var order = await _repository.GetByIdAsync(request.OrderId, cancellationToken)
                ?? throw new InvalidOperationException($"Order {request.OrderId} not found");

            order.CompleteReturn(request.RefundAmount, request.RefundShippingFee);
            await _repository.UpdateAsync(order, cancellationToken);

            return SalesOrderMapper.MapToDto(order);
        }
    }
}
