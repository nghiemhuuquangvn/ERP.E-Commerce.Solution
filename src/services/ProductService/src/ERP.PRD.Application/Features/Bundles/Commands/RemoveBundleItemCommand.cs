using ERP.PRD.Application.Interfaces;
using MediatR;

namespace ERP.PRD.Application.Features.Bundles.Commands
{
    public class RemoveBundleItemCommand : IRequest<Unit>
    {
        public Guid BundleItemId { get; set; }
    }

    public class RemoveBundleItemHandler : IRequestHandler<RemoveBundleItemCommand, Unit>
    {
        private readonly IProductUnitOfWork _uow;

        public RemoveBundleItemHandler(IProductUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<Unit> Handle(RemoveBundleItemCommand request, CancellationToken cancellationToken)
        {
            var item = await _uow.Bundles.FindByIdAsync(request.BundleItemId, cancellationToken);
            if (item == null)
                throw new KeyNotFoundException($"Bundle item with ID {request.BundleItemId} not found");

            _uow.Bundles.Remove(item);
            await _uow.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}
