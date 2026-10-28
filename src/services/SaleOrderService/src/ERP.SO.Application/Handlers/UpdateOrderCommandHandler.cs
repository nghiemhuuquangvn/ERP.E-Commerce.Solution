using ERP.SO.Application.Commands;
using ERP.SO.Application.DTOs;
using ERP.SO.Application.Interfaces;
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

    /// <summary>
    /// Approving a return asks Shipping to book the goods' way back at a carrier (ROADMAP P1-11d). Shipping unreachable: the approval is refused, a person tries again (nothing
    /// was changed). Shipping answered but booked nothing (nothing went through it, no pickup address, every carrier refused): the return is still approved, the reason is kept,
    /// and the goods are received by hand as before.
    /// </summary>
    public class ApproveReturnCommandHandler : IRequestHandler<ApproveReturnCommand, SalesOrderDto>
    {
        private readonly ISalesOrderRepository _repository;
        private readonly IShippingServiceClient _shipping;

        public ApproveReturnCommandHandler(ISalesOrderRepository repository, IShippingServiceClient shipping)
        {
            _repository = repository;
            _shipping = shipping;
        }

        public async Task<SalesOrderDto> Handle(ApproveReturnCommand request, CancellationToken cancellationToken)
        {
            var order = await _repository.GetByIdAsync(request.OrderId, cancellationToken)
                ?? throw new InvalidOperationException($"Order {request.OrderId} not found");
            if (order.Status != ERP.SO.Domain.Entities.OrderStatus.ReturnRequested)
                throw new InvalidOperationException($"Cannot approve a return for an order in {order.Status} status");

            var answer = await _shipping.BookReturnAsync(order.Id, order.ReturnReason ?? string.Empty, cancellationToken)
                ?? throw new ERP.Shared.Infrastructure.Exceptions.BusinessRuleException("Sales", "Shipping cannot be reached right now, the return was not approved. Please try again in a moment.");

            var note = answer.Error ?? (answer.Parcels == 0 ? "no reverse parcel" : $"{answer.Booked} of {answer.Parcels} reverse parcel(s) booked at the carrier");
            order.ApproveReturn(answer.Booked > 0, note);
            await _repository.UpdateAsync(order, cancellationToken);
            return SalesOrderMapper.MapToDto(order);
        }
    }

    public class MarkReturnGoodsReceivedCommandHandler : IRequestHandler<MarkReturnGoodsReceivedCommand, Unit>
    {
        private readonly ISalesOrderRepository _repository;

        public MarkReturnGoodsReceivedCommandHandler(ISalesOrderRepository repository) => _repository = repository;

        public async Task<Unit> Handle(MarkReturnGoodsReceivedCommand request, CancellationToken cancellationToken)
        {
            var order = await _repository.GetByIdAsync(request.OrderId, cancellationToken);
            if (order is null) return Unit.Value;
            order.MarkReturnGoodsReceived();
            await _repository.UpdateAsync(order, cancellationToken);
            return Unit.Value;
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
