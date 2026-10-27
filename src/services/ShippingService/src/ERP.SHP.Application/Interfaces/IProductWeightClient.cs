namespace ERP.SHP.Application.Interfaces;

/// <summary>
/// The weight of one unit of each product, asked of ProductService (internal call, ROADMAP P1-11b). A product with no weight is simply absent from the answer;
/// when ProductService cannot be reached the answer is empty - a booking never fails because a weight could not be looked up, the shop's default item weight is used.
/// </summary>
public interface IProductWeightClient
{
    /// <returns>Grams per unit by product id.</returns>
    Task<IReadOnlyDictionary<Guid, int>> GetWeightsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken ct);
}
