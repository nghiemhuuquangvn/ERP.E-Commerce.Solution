using ERP.PAY.Application.DTOs;
using MediatR;

namespace ERP.PAY.Application.Commands;

public class SavePaymentMethodCommand : IRequest<SavedPaymentMethodDto>
{
    public Guid CustomerId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Last4 { get; set; }
    public int? ExpiryMonth { get; set; }
    public int? ExpiryYear { get; set; }
}

public class SetDefaultPaymentMethodCommand : IRequest<SavedPaymentMethodDto>
{
    public Guid PaymentMethodId { get; set; }
    public Guid CustomerId { get; set; }
}

public class RemovePaymentMethodCommand : IRequest
{
    public Guid PaymentMethodId { get; set; }
}
