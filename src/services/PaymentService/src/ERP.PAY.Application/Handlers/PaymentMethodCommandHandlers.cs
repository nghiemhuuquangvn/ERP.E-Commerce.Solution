using ERP.PAY.Application.Commands;
using ERP.PAY.Application.DTOs;
using ERP.PAY.Application.Interfaces;
using ERP.PAY.Domain.Entities;
using MediatR;

namespace ERP.PAY.Application.Handlers;

public class SavePaymentMethodCommandHandler : IRequestHandler<SavePaymentMethodCommand, SavedPaymentMethodDto>
{
    private readonly ISavedPaymentMethodRepository _repository;
    public SavePaymentMethodCommandHandler(ISavedPaymentMethodRepository repository) => _repository = repository;

    public async Task<SavedPaymentMethodDto> Handle(SavePaymentMethodCommand request, CancellationToken cancellationToken)
    {
        var method = SavedPaymentMethod.Create(
            request.CustomerId, request.Provider, request.Token, request.Brand, request.Last4, request.ExpiryMonth, request.ExpiryYear);

        var existing = await _repository.GetByCustomerIdAsync(request.CustomerId, cancellationToken);
        if (existing.Count == 0)
            method.SetAsDefault(); // first saved method for a customer becomes the default automatically

        await _repository.AddAsync(method, cancellationToken);
        return MapToDto(method);
    }

    internal static SavedPaymentMethodDto MapToDto(SavedPaymentMethod m) => new()
    {
        Id = m.Id,
        CustomerId = m.CustomerId,
        Provider = m.Provider,
        Brand = m.Brand,
        Last4 = m.Last4,
        ExpiryMonth = m.ExpiryMonth,
        ExpiryYear = m.ExpiryYear,
        IsDefault = m.IsDefault,
        IsActive = m.IsActive,
        CreatedAt = m.CreatedAt
    };
}

public class SetDefaultPaymentMethodCommandHandler : IRequestHandler<SetDefaultPaymentMethodCommand, SavedPaymentMethodDto>
{
    private readonly ISavedPaymentMethodRepository _repository;
    public SetDefaultPaymentMethodCommandHandler(ISavedPaymentMethodRepository repository) => _repository = repository;

    public async Task<SavedPaymentMethodDto> Handle(SetDefaultPaymentMethodCommand request, CancellationToken cancellationToken)
    {
        var target = await _repository.GetByIdAsync(request.PaymentMethodId, cancellationToken)
            ?? throw new InvalidOperationException($"Payment method {request.PaymentMethodId} not found.");

        var siblings = await _repository.GetByCustomerIdAsync(request.CustomerId, cancellationToken);
        foreach (var sibling in siblings.Where(s => s.Id != target.Id && s.IsDefault))
        {
            sibling.ClearDefault();
            await _repository.UpdateAsync(sibling, cancellationToken);
        }

        target.SetAsDefault();
        await _repository.UpdateAsync(target, cancellationToken);
        return SavePaymentMethodCommandHandler.MapToDto(target);
    }
}

public class RemovePaymentMethodCommandHandler : IRequestHandler<RemovePaymentMethodCommand>
{
    private readonly ISavedPaymentMethodRepository _repository;
    public RemovePaymentMethodCommandHandler(ISavedPaymentMethodRepository repository) => _repository = repository;

    public async Task Handle(RemovePaymentMethodCommand request, CancellationToken cancellationToken)
    {
        var method = await _repository.GetByIdAsync(request.PaymentMethodId, cancellationToken)
            ?? throw new InvalidOperationException($"Payment method {request.PaymentMethodId} not found.");

        method.Deactivate();
        await _repository.UpdateAsync(method, cancellationToken);
    }
}
