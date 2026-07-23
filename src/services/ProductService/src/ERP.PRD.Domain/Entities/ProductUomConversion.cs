using ERP.Shared.Core.Domain;

namespace ERP.PRD.Domain.Entities
{
    /// <summary>
    /// Per-product unit-of-measure conversion (e.g. 1 Case = 24 Can). FromUomId/ToUomId
    /// reference SharedMasterDataService's UnitOfMeasure by Id only (cross-service,
    /// no database FK — see CLAUDE.md database ownership rules).
    /// </summary>
    public class ProductUomConversion : BaseEntity
    {
        public Guid ProductId { get; set; }
        public Guid FromUomId { get; set; }
        public Guid ToUomId { get; set; }
        public decimal ConversionFactor { get; set; }

        public ProductUomConversion() { }

        public ProductUomConversion(Guid productId, Guid fromUomId, Guid toUomId, decimal conversionFactor)
        {
            Id = Guid.NewGuid();
            ProductId = productId;
            FromUomId = fromUomId;
            ToUomId = toUomId;
            ConversionFactor = conversionFactor;
            CreatedAt = DateTime.UtcNow;
        }

        public void Update(decimal conversionFactor)
        {
            ConversionFactor = conversionFactor;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
