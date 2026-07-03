using ERP.PAY.Application.DTOs;
using ERP.PAY.Application.Handlers;
using ERP.PAY.Application.Interfaces;
using MediatR;

namespace ERP.PAY.Application.Queries;

public class GetPaymentMethodsByCustomerQuery : IRequest<List<SavedPaymentMethodDto>>
{
    public Guid CustomerId { get; set; }
}

public class GetPaymentMethodsByCustomerQueryHandler : IRequestHandler<GetPaymentMethodsByCustomerQuery, List<SavedPaymentMethodDto>>
{
    private readonly ISavedPaymentMethodRepository _repository;
    public GetPaymentMethodsByCustomerQueryHandler(ISavedPaymentMethodRepository repository) => _repository = repository;

    public async Task<List<SavedPaymentMethodDto>> Handle(GetPaymentMethodsByCustomerQuery request, CancellationToken cancellationToken)
    {
        var methods = await _repository.GetByCustomerIdAsync(request.CustomerId, cancellationToken);
        return methods.Where(m => m.IsActive).Select(SavePaymentMethodCommandHandler.MapToDto).ToList();
    }
}
