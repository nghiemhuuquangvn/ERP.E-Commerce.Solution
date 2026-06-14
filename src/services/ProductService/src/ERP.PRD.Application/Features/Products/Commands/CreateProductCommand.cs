using ERP.Shared.Messaging;
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
        private readonly IRepository<Domain.Entities.Product> _repository;
        private readonly IUnitOfWork _unitOfWork;

        public CreateProductHandler(IRepository<Domain.Entities.Product> repository, IUnitOfWork unitOfWork)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
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

            await _repository.AddAsync(product, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

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
