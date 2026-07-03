using MediatR;

namespace ERP.PO.Application.Features.Suppliers.Queries;

public class GetSupplierRatingsQuery : IRequest<SupplierRatingsResult>
{
    public Guid SupplierId { get; set; }
}

public class SupplierRatingDto
{
    public Guid Id { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public string RatedBy { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
}

public class SupplierRatingsResult
{
    public double AverageRating { get; set; }
    public int TotalRatings { get; set; }
    public List<SupplierRatingDto> Ratings { get; set; } = new();
}
