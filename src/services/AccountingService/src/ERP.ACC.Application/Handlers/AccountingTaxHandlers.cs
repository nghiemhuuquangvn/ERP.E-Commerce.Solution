using ERP.ACC.Application.Commands;
using ERP.ACC.Application.DTOs;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Queries;
using ERP.ACC.Domain.Entities;
using MediatR;

namespace ERP.ACC.Application.Handlers
{
    public class CreateTaxRateCommandHandler : IRequestHandler<CreateTaxRateCommand, TaxRateDto>
    {
        private readonly ITaxRateRepository _taxRates;

        public CreateTaxRateCommandHandler(ITaxRateRepository taxRates) => _taxRates = taxRates;

        public async Task<TaxRateDto> Handle(CreateTaxRateCommand request, CancellationToken cancellationToken)
        {
            var taxRate = TaxRate.Create(request.Name, request.Code, request.Rate, request.Description);
            await _taxRates.AddAsync(taxRate, cancellationToken);
            return MapToDto(taxRate);
        }

        internal static TaxRateDto MapToDto(TaxRate t) => new()
        {
            Id = t.Id,
            Name = t.Name,
            Code = t.Code,
            Rate = t.Rate,
            IsActive = t.IsActive,
            Description = t.Description,
            CreatedAt = t.CreatedAt
        };
    }

    public class UpdateTaxRateCommandHandler : IRequestHandler<UpdateTaxRateCommand, TaxRateDto>
    {
        private readonly ITaxRateRepository _taxRates;

        public UpdateTaxRateCommandHandler(ITaxRateRepository taxRates) => _taxRates = taxRates;

        public async Task<TaxRateDto> Handle(UpdateTaxRateCommand request, CancellationToken cancellationToken)
        {
            var taxRate = await _taxRates.GetByIdAsync(request.TaxRateId, cancellationToken)
                ?? throw new InvalidOperationException($"Tax rate {request.TaxRateId} not found.");

            taxRate.Update(request.Name, request.Code, request.Rate, request.Description);
            await _taxRates.UpdateAsync(taxRate, cancellationToken);
            return CreateTaxRateCommandHandler.MapToDto(taxRate);
        }
    }

    public class DeactivateTaxRateCommandHandler : IRequestHandler<DeactivateTaxRateCommand, TaxRateDto>
    {
        private readonly ITaxRateRepository _taxRates;

        public DeactivateTaxRateCommandHandler(ITaxRateRepository taxRates) => _taxRates = taxRates;

        public async Task<TaxRateDto> Handle(DeactivateTaxRateCommand request, CancellationToken cancellationToken)
        {
            var taxRate = await _taxRates.GetByIdAsync(request.TaxRateId, cancellationToken)
                ?? throw new InvalidOperationException($"Tax rate {request.TaxRateId} not found.");

            taxRate.Deactivate();
            await _taxRates.UpdateAsync(taxRate, cancellationToken);
            return CreateTaxRateCommandHandler.MapToDto(taxRate);
        }
    }

    public class ListTaxRatesQueryHandler : IRequestHandler<ListTaxRatesQuery, List<TaxRateDto>>
    {
        private readonly ITaxRateRepository _taxRates;

        public ListTaxRatesQueryHandler(ITaxRateRepository taxRates) => _taxRates = taxRates;

        public async Task<List<TaxRateDto>> Handle(ListTaxRatesQuery request, CancellationToken cancellationToken)
        {
            var rates = await _taxRates.GetAllAsync(request.ActiveOnly, cancellationToken);
            return rates.Select(CreateTaxRateCommandHandler.MapToDto).ToList();
        }
    }

    public class GetTaxRateQueryHandler : IRequestHandler<GetTaxRateQuery, TaxRateDto?>
    {
        private readonly ITaxRateRepository _taxRates;

        public GetTaxRateQueryHandler(ITaxRateRepository taxRates) => _taxRates = taxRates;

        public async Task<TaxRateDto?> Handle(GetTaxRateQuery request, CancellationToken cancellationToken)
        {
            var taxRate = await _taxRates.GetByIdAsync(request.TaxRateId, cancellationToken);
            return taxRate == null ? null : CreateTaxRateCommandHandler.MapToDto(taxRate);
        }
    }
}
