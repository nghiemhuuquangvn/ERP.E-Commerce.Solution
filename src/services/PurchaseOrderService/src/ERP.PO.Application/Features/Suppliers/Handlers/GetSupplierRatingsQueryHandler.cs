using MediatR;
using ERP.PO.Application.Features.Suppliers.Queries;
using ERP.PO.Domain.Entities;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Data;

namespace ERP.PO.Application.Features.Suppliers.Handlers;

public class GetSupplierRatingsQueryHandler : IRequestHandler<GetSupplierRatingsQuery, SupplierRatingsResult>
{
    private readonly IRepository<SupplierRating> _repository;
    private readonly IUserContextService _userContextService;

    public GetSupplierRatingsQueryHandler(IRepository<SupplierRating> repository, IUserContextService userContextService)
    {
        _repository = repository;
        _userContextService = userContextService;
    }

    public async Task<SupplierRatingsResult> Handle(GetSupplierRatingsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = new Guid(_userContextService.GetCurrentUser().TenantId);

        var ratings = (await _repository.GetAllAsync(tenantId, cancellationToken))
            .Where(r => r.SupplierId == request.SupplierId)
            .OrderByDescending(r => r.CreatedAt)
            .ToList();

        return new SupplierRatingsResult
        {
            AverageRating = ratings.Count == 0 ? 0 : Math.Round(ratings.Average(r => r.Rating), 2),
            TotalRatings = ratings.Count,
            Ratings = ratings.Select(r => new SupplierRatingDto
            {
                Id = r.Id,
                Rating = r.Rating,
                Comment = r.Comment,
                RatedBy = r.RatedBy,
                CreatedAt = r.CreatedAt
            }).ToList()
        };
    }
}
