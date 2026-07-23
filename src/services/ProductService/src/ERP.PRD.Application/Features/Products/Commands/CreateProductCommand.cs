using ERP.PRD.Application.Interfaces;
using MediatR;

namespace ERP.PRD.Application.Features.Products.Commands
{
    public class CreateProductCommand : IRequest<CreateProductResponse>
    {
        public string Name { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int ProductType { get; set; }
        public Guid? CategoryId { get; set; }
        public Guid? BrandId { get; set; }

        /// <summary>
        /// References SharedMasterDataService's UnitOfMeasure by Id only (cross-service,
        /// no database FK — see CLAUDE.md database ownership rules).
        /// </summary>
        public Guid? BaseUomId { get; set; }
    }

    public class CreateProductResponse
    {
        public Guid ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public int Status { get; set; }
    }

    public class CreateProductHandler : IRequestHandler<CreateProductCommand, CreateProductResponse>
    {
        private readonly IProductUnitOfWork _uow;

        public CreateProductHandler(IProductUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<CreateProductResponse> Handle(CreateProductCommand request, CancellationToken cancellationToken)
        {
            var product = Domain.Entities.Product.Create(
                request.Name,
                request.Sku,
                (Domain.Enums.ProductType)request.ProductType,
                request.CategoryId
            );

            if (!string.IsNullOrWhiteSpace(request.Description))
                product.UpdateDescription(request.Description);

            if (request.BrandId.HasValue)
            {
                var brandExists = await _uow.Brands.ExistsAsync(request.BrandId.Value, cancellationToken);
                if (!brandExists)
                    throw new KeyNotFoundException($"Brand with ID {request.BrandId} not found");

                product.SetBrand(request.BrandId);
            }

            if (request.BaseUomId.HasValue)
                product.SetBaseUom(request.BaseUomId);

            await _uow.Products.AddAsync(product, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);

            return new CreateProductResponse
            {
                ProductId = product.Id,
                Name = product.Name,
                Sku = product.Sku,
                Status = (int)product.Status
            };
        }
    }
}
