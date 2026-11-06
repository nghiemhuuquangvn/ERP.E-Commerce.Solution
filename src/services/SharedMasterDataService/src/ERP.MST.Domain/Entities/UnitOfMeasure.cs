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
        /// <summary>Decimals a quantity in this unit may have (0 = whole units). Manufacturing rounds calculated requirements UP to it.</summary>
        public int DecimalPlaces { get; set; }

        public static UnitOfMeasure Create(
            string code,
            string name,
            string symbol,
            int unitType,
            Guid? baseUnitId = null,
            decimal conversionFactor = 1m,
            int decimalPlaces = 0)
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
                DecimalPlaces = decimalPlaces,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            unitOfMeasure.AddDomainEvent(new UnitOfMeasureCreatedEvent(unitOfMeasure.Id, unitOfMeasure.Code, unitOfMeasure.Name));

            return unitOfMeasure;
        }

        public void Update(string name, string symbol, decimal conversionFactor, int? decimalPlaces = null)
        {
            if (decimalPlaces.HasValue) DecimalPlaces = decimalPlaces.Value;
            Name = name;
            Symbol = symbol;
            ConversionFactor = conversionFactor;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
