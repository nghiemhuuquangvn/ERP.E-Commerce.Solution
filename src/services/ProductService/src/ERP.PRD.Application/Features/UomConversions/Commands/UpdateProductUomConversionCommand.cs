using ERP.PRD.Application.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.PRD.Application.Features.UomConversions.Commands
{
    public class UpdateProductUomConversionCommand : IRequest
    {
        public Guid ProductId { get; set; }
        public Guid UomConversionId { get; set; }
        public decimal ConversionFactor { get; set; }
    }

    public class UpdateProductUomConversionValidator : AbstractValidator<UpdateProductUomConversionCommand>
    {
        public UpdateProductUomConversionValidator()
        {
            RuleFor(x => x.ProductId)
                .NotEqual(Guid.Empty).WithMessage("Product ID is required");

            RuleFor(x => x.UomConversionId)
                .NotEqual(Guid.Empty).WithMessage("UoM conversion ID is required");

            RuleFor(x => x.ConversionFactor)
                .GreaterThan(0).WithMessage("Conversion factor must be greater than 0");
        }
    }

    public class UpdateProductUomConversionHandler : IRequestHandler<UpdateProductUomConversionCommand>
    {
        private readonly IProductUnitOfWork _uow;
        private readonly ILogger<UpdateProductUomConversionHandler> _logger;

        public UpdateProductUomConversionHandler(IProductUnitOfWork uow, ILogger<UpdateProductUomConversionHandler> logger)
        {
            _uow = uow;
            _logger = logger;
        }

        public async Task Handle(UpdateProductUomConversionCommand request, CancellationToken cancellationToken)
        {
            var conversion = await _uow.UomConversions.GetByIdAsync(request.UomConversionId, cancellationToken);
            if (conversion == null || conversion.ProductId != request.ProductId)
                throw new KeyNotFoundException(
                    $"UoM conversion with ID {request.UomConversionId} not found for product {request.ProductId}");

            conversion.Update(request.ConversionFactor);
            _uow.UomConversions.Update(conversion);

            await _uow.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("UoM conversion updated: {UomConversionId}", request.UomConversionId);
        }
    }
}
