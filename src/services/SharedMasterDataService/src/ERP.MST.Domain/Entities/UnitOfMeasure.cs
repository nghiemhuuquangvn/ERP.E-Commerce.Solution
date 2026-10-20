using ERP.MST.Domain.Events;
using ERP.Shared.Core.Domain;

namespace ERP.MST.Domain.Entities
{
    public class UnitOfMeasure : AggregateRoot, ISharedReferenceData
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public int UnitType { get; set; }
        public Guid? BaseUnitId { get; set; }
        public decimal ConversionFactor { get; set; } = 1m;

        public static UnitOfMeasure Create(
            string code,
            string name,
            string symbol,
            int unitType,
            Guid? baseUnitId = null,
            decimal conversionFactor = 1m)
        {
            var unitOfMeasure = new UnitOfMeasure
            {
                Id = Guid.NewGuid(),
                Code = code,
                Name = name,
                Symbol = symbol,
                UnitType = unitType,
                BaseUnitId = baseUnitId,
                ConversionFactor = conversionFactor,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            unitOfMeasure.AddDomainEvent(new UnitOfMeasureCreatedEvent(unitOfMeasure.Id, unitOfMeasure.Code, unitOfMeasure.Name));

            return unitOfMeasure;
        }

        public void Update(string name, string symbol, decimal conversionFactor)
        {
            Name = name;
            Symbol = symbol;
            ConversionFactor = conversionFactor;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
