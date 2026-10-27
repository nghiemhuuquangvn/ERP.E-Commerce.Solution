using ERP.Shared.Authorization;
using ERP.SO.Application.Commands;
using ERP.SO.Application.DTOs;
using ERP.SO.Application.Interfaces;
using ERP.SO.Domain.Entities;
using ERP.SO.Domain.ValueObjects;
using MediatR;

namespace ERP.SO.Application.Handlers
{
    public class CreateQuotationCommandHandler : IRequestHandler<CreateQuotationCommand, SalesQuotationDto>
    {
        private readonly ISalesQuotationRepository _repository;
        private readonly IOrderNumberGenerator _orderNumberGenerator;
        private readonly IUserContextService _userContextService;

        public CreateQuotationCommandHandler(ISalesQuotationRepository repository, IOrderNumberGenerator orderNumberGenerator, IUserContextService userContextService)
        {
            _repository = repository;
            _orderNumberGenerator = orderNumberGenerator;
            _userContextService = userContextService;
        }

        public async Task<SalesQuotationDto> Handle(CreateQuotationCommand request, CancellationToken cancellationToken)
        {
            var tenantId = new Guid(_userContextService.GetCurrentUser().TenantId);
            var quotationNo = await _orderNumberGenerator.NextAsync(tenantId, "QT", cancellationToken);

            var lineItems = request.LineItems
                .Select(li => new QuotationLineItem(li.ProductId, li.ProductName, li.Quantity, li.UnitPrice))
                .ToList();

            var quotation = SalesQuotation.Create(
                request.CustomerId,
                request.CustomerName,
                request.CustomerEmail,
                lineItems,
                request.ValidUntil,
                quotationNo);

            await _repository.AddAsync(quotation, cancellationToken);

            return SalesQuotationMapper.MapToDto(quotation);
        }
    }

    public class SendQuotationCommandHandler : IRequestHandler<SendQuotationCommand, SalesQuotationDto>
    {
        private readonly ISalesQuotationRepository _repository;

        public SendQuotationCommandHandler(ISalesQuotationRepository repository) => _repository = repository;

        public async Task<SalesQuotationDto> Handle(SendQuotationCommand request, CancellationToken cancellationToken)
        {
            var quotation = await _repository.GetByIdAsync(request.QuotationId, cancellationToken)
                ?? throw new InvalidOperationException($"Quotation {request.QuotationId} not found");

            quotation.Send();
            await _repository.UpdateAsync(quotation, cancellationToken);

            return SalesQuotationMapper.MapToDto(quotation);
        }
    }

    public class AcceptQuotationCommandHandler : IRequestHandler<AcceptQuotationCommand, SalesQuotationDto>
    {
        private readonly ISalesQuotationRepository _repository;

        public AcceptQuotationCommandHandler(ISalesQuotationRepository repository) => _repository = repository;

        public async Task<SalesQuotationDto> Handle(AcceptQuotationCommand request, CancellationToken cancellationToken)
        {
            var quotation = await _repository.GetByIdAsync(request.QuotationId, cancellationToken)
                ?? throw new InvalidOperationException($"Quotation {request.QuotationId} not found");

            quotation.Accept();
            await _repository.UpdateAsync(quotation, cancellationToken);

            return SalesQuotationMapper.MapToDto(quotation);
        }
    }

    public class RejectQuotationCommandHandler : IRequestHandler<RejectQuotationCommand, SalesQuotationDto>
    {
        private readonly ISalesQuotationRepository _repository;

        public RejectQuotationCommandHandler(ISalesQuotationRepository repository) => _repository = repository;

        public async Task<SalesQuotationDto> Handle(RejectQuotationCommand request, CancellationToken cancellationToken)
        {
            var quotation = await _repository.GetByIdAsync(request.QuotationId, cancellationToken)
                ?? throw new InvalidOperationException($"Quotation {request.QuotationId} not found");

            quotation.Reject();
            await _repository.UpdateAsync(quotation, cancellationToken);

            return SalesQuotationMapper.MapToDto(quotation);
        }
    }

    public class ConvertQuotationToOrderCommandHandler : IRequestHandler<ConvertQuotationToOrderCommand, SalesOrderDto>
    {
        private readonly ISalesQuotationRepository _quotations;
        private readonly ISalesOrderRepository _orders;
        private readonly IOrderNumberGenerator _orderNumberGenerator;
        private readonly IUserContextService _userContextService;

        public ConvertQuotationToOrderCommandHandler(
            ISalesQuotationRepository quotations,
            ISalesOrderRepository orders,
            IOrderNumberGenerator orderNumberGenerator,
            IUserContextService userContextService)
        {
            _quotations = quotations;
            _orders = orders;
            _orderNumberGenerator = orderNumberGenerator;
            _userContextService = userContextService;
        }

        public async Task<SalesOrderDto> Handle(ConvertQuotationToOrderCommand request, CancellationToken cancellationToken)
        {
            var quotation = await _quotations.GetByIdAsync(request.QuotationId, cancellationToken)
                ?? throw new InvalidOperationException($"Quotation {request.QuotationId} not found");

            var address = new Address(
                request.ShippingAddress.Street,
                request.ShippingAddress.City,
                request.ShippingAddress.State,
                request.ShippingAddress.PostalCode,
                request.ShippingAddress.Country,
                request.ShippingAddress.Ward);

            var lineItems = quotation.LineItems
                .Select(li => new OrderLineItem(li.ProductId, li.ProductName, li.Quantity, li.UnitPrice))
                .ToList();

            var tenantId = new Guid(_userContextService.GetCurrentUser().TenantId);
            var orderNo = await _orderNumberGenerator.NextAsync(tenantId, "SO", cancellationToken);

            var order = SalesOrder.Create(
                quotation.CustomerId,
                quotation.CustomerName,
                quotation.CustomerEmail,
                address,
                lineItems,
                notes: null,
                orderNo: orderNo,
                orderSource: OrderSource.Manual,
                quotationId: quotation.Id);

            await _orders.AddAsync(order, cancellationToken);

            quotation.MarkConverted(order.Id);
            await _quotations.UpdateAsync(quotation, cancellationToken);

            return SalesOrderMapper.MapToDto(order);
        }
    }
}
