using ERP.PRD.Application.Interfaces;
using ERP.PRD.Domain.Entities;
using FluentValidation;
using MediatR;

namespace ERP.PRD.Application.Features.UomConversions.Commands
{
    public class CreateProductUomConversionCommand : IRequest<CreateProductUomConversionResponse>
    {
        public Guid ProductId { get; set; }
        public Guid FromUomId { get; set; }
        public Guid ToUomId { get; set; }
        public decimal ConversionFactor { get; set; }
    }

    public class CreateProductUomConversionResponse
    {
        public Guid UomConversionId { get; set; }
        public Guid ProductId { get; set; }
        public Guid FromUomId { get; set; }
        public Guid ToUomId { get; set; }
        public decimal ConversionFactor { get; set; }
    }

    public class CreateProductUomConversionValidator : AbstractValidator<CreateProductUomConversionCommand>
    {
        public CreateProductUomConversionValidator()
        {
            RuleFor(x => x.ProductId)
                .NotEqual(Guid.Empty).WithMessage("Product ID is required");

            RuleFor(x => x.FromUomId)
                .NotEqual(Guid.Empty).WithMessage("From UoM ID is required");

            RuleFor(x => x.ToUomId)
                .NotEqual(Guid.Empty).WithMessage("To UoM ID is required");

            RuleFor(x => x.ConversionFactor)
                .GreaterThan(0).WithMessage("Conversion factor must be greater than 0");

            RuleFor(x => x)
                .Custom((x, context) =>
                {
                    if (x.FromUomId == x.ToUomId)
                        context.AddFailure("ToUomId", "From and To UoM must be different");
                });
        }
    }

    public class CreateProductUomConversionHandler
        : IRequestHandler<CreateProductUomConversionCommand, CreateProductUomConversionResponse>
    {
        private readonly IProductUnitOfWork _uow;
        private readonly ILogger<CreateProductUomConversionHandler> _logger;

        public CreateProductUomConversionHandler(IProductUnitOfWork uow, ILogger<CreateProductUomConversionHandler> logger)
        {
            _uow = uow;
            _logger = logger;
        }

        public async Task<CreateProductUomConversionResponse> Handle(
            CreateProductUomConversionCommand request, CancellationToken cancellationToken)
        {
            var productExists = await _uow.Products.ExistsAsync(request.ProductId, cancellationToken);
            if (!productExists)
                throw new KeyNotFoundException($"Product with ID {request.ProductId} not found");

            var pairExists = await _uow.UomConversions.PairExistsAsync(
                request.ProductId, request.FromUomId, request.ToUomId, cancellationToken);
            if (pairExists)
                throw new InvalidOperationException(
                    $"A conversion from {request.FromUomId} to {request.ToUomId} already exists for this product");

            var conversion = new ProductUomConversion(
                request.ProductId, request.FromUomId, request.ToUomId, request.ConversionFactor);

            await _uow.UomConversions.AddAsync(conversion, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "UoM conversion created for product {ProductId}: {UomConversionId}", request.ProductId, conversion.Id);

            return new CreateProductUomConversionResponse
            {
                UomConversionId = conversion.Id,
                ProductId = conversion.ProductId,
                FromUomId = conversion.FromUomId,
                ToUomId = conversion.ToUomId,
                ConversionFactor = conversion.ConversionFactor
            };
        }
    }
}
