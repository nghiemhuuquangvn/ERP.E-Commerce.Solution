using ERP.PRD.Domain.Enums;
using ERP.PRD.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
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
        private readonly ProductDbContext _context;

        public GetProductAttributeValuesHandler(ProductDbContext context)
        {
            _context = context;
        }

        public async Task<List<AttributeValueDto>> Handle(GetProductAttributeValuesQuery request, CancellationToken cancellationToken)
        {
            var productExists = await _context.Products
                .AnyAsync(p => p.Id == request.ProductId && !p.IsDeleted, cancellationToken);

            if (!productExists)
                throw new KeyNotFoundException($"Product with ID {request.ProductId} not found");

            // A variant should show its own specific attribute values plus the parent product's
            // shared ones, so when VariantId is provided we pull both owner rows; otherwise only
            // the Product-owned values.
            var values = request.VariantId.HasValue
                ? await _context.ProductAttributeValues
                    .Where(v => !v.IsDeleted &&
                        ((v.OwnerType == AttributeOwnerType.Product && v.OwnerId == request.ProductId) ||
                         (v.OwnerType == AttributeOwnerType.ProductVariant && v.OwnerId == request.VariantId.Value)))
                    .ToListAsync(cancellationToken)
                : await _context.ProductAttributeValues
                    .Where(v => !v.IsDeleted && v.OwnerType == AttributeOwnerType.Product && v.OwnerId == request.ProductId)
                    .ToListAsync(cancellationToken);

            if (values.Count == 0)
                return new List<AttributeValueDto>();

            var attributeIds = values.Select(v => v.ProductAttributeId).Distinct().ToList();
            var attributes = await _context.ProductAttributes
                .Include(a => a.Options)
                .Where(a => attributeIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, cancellationToken);

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
