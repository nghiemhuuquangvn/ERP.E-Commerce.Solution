using ERP.PRD.Application.Interfaces;
using ERP.PRD.Domain.Entities;
using MediatR;

namespace ERP.PRD.Application.Features.Bundles.Commands
{
    public class AddBundleItemCommand : IRequest<Guid>
    {
        public Guid BundleProductId { get; set; }
        public Guid ComponentProductId { get; set; }
        public int Quantity { get; set; } = 1;
    }

    public class AddBundleItemHandler : IRequestHandler<AddBundleItemCommand, Guid>
    {
        private readonly IProductUnitOfWork _uow;

        public AddBundleItemHandler(IProductUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<Guid> Handle(AddBundleItemCommand request, CancellationToken cancellationToken)
        {
            var bundleExists = await _uow.Bundles.ProductExistsAsync(request.BundleProductId, cancellationToken);
            if (!bundleExists)
                throw new KeyNotFoundException($"Product with ID {request.BundleProductId} not found");

            var componentExists = await _uow.Bundles.ProductExistsAsync(request.ComponentProductId, cancellationToken);
            if (!componentExists)
                throw new KeyNotFoundException($"Component product with ID {request.ComponentProductId} not found");

            var item = ProductBundleItem.Create(request.BundleProductId, request.ComponentProductId, request.Quantity);
            await _uow.Bundles.AddAsync(item, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);

            return item.Id;
        }
    }
}
