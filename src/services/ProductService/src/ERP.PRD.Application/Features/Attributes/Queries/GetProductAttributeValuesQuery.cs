using ERP.PRD.Application.Interfaces;
using MediatR;
using Newtonsoft.Json;

namespace ERP.PRD.Application.Features.Attributes.Queries
{
    public class GetProductAttributeValuesQuery : IRequest<List<AttributeValueDto>>
    {
        public Guid ProductId { get; set; }
        public Guid? VariantId { get; set; }
    }

    public class AttributeValueDto
    {
        public Guid AttributeId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int DataType { get; set; }
        public string? Unit { get; set; }
        public string? ValueText { get; set; }
        public decimal? ValueNumber { get; set; }
        public bool? ValueBoolean { get; set; }
        public DateTime? ValueDate { get; set; }
        public string? OptionValue { get; set; }
        public List<string> OptionValues { get; set; } = new();
    }

    public class GetProductAttributeValuesHandler : IRequestHandler<GetProductAttributeValuesQuery, List<AttributeValueDto>>
    {
        private readonly IProductUnitOfWork _uow;

        public GetProductAttributeValuesHandler(IProductUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<List<AttributeValueDto>> Handle(GetProductAttributeValuesQuery request, CancellationToken cancellationToken)
        {
            var productExists = await _uow.Products.ExistsAsync(request.ProductId, cancellationToken);

            if (!productExists)
                throw new KeyNotFoundException($"Product with ID {request.ProductId} not found");

            var values = await _uow.AttributeValues.GetByOwnerAsync(
                request.ProductId, request.VariantId, cancellationToken);

            if (values.Count == 0)
                return new List<AttributeValueDto>();

            var attributeIds = values.Select(v => v.ProductAttributeId).Distinct().ToList();
            var attributes = await _uow.Attributes.GetByIdsWithOptionsAsDictionaryAsync(attributeIds, cancellationToken);

            var result = new List<AttributeValueDto>();

            foreach (var value in values)
            {
                if (!attributes.TryGetValue(value.ProductAttributeId, out var attribute))
                    continue;

                var dto = new AttributeValueDto
                {
                    AttributeId = attribute.Id,
                    Code = attribute.Code,
                    Name = attribute.Name,
                    DataType = (int)attribute.DataType,
                    Unit = attribute.Unit,
                    ValueText = value.ValueText,
                    ValueNumber = value.ValueNumber,
                    ValueBoolean = value.ValueBoolean,
                    ValueDate = value.ValueDate
                };

                if (value.ProductAttributeOptionId.HasValue)
                {
                    dto.OptionValue = attribute.Options
                        .FirstOrDefault(o => o.Id == value.ProductAttributeOptionId.Value)?.Value;
                }

                if (!string.IsNullOrWhiteSpace(value.ValueOptionIdsJson))
                {
                    var optionIds = JsonConvert.DeserializeObject<List<Guid>>(value.ValueOptionIdsJson) ?? new List<Guid>();
                    dto.OptionValues = attribute.Options
                        .Where(o => optionIds.Contains(o.Id))
                        .Select(o => o.Value)
                        .ToList();
                }

                result.Add(dto);
            }

            return result;
        }
    }
}
