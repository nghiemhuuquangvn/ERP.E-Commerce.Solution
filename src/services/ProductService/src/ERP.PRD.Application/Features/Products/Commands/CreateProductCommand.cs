using ERP.PRD.Infrastructure.Persistence;
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
        private readonly ProductDbContext _context;

        public CreateProductHandler(ProductDbContext context)
        {
            _context = context;
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

            _context.Products.Add(product);
            await _context.SaveChangesAsync(cancellationToken);

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
