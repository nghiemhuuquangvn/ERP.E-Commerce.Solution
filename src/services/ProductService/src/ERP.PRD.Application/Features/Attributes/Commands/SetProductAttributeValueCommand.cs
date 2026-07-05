using ERP.PRD.Application.Interfaces;
using ERP.PRD.Domain.Entities;
using ERP.PRD.Domain.Enums;
using MediatR;
using FluentValidation;
using Newtonsoft.Json;

namespace ERP.PRD.Application.Features.Attributes.Commands
{
    public class SetProductAttributeValueCommand : IRequest<Unit>
    {
        public Guid ProductAttributeId { get; set; }
        public int OwnerType { get; set; }
        public Guid OwnerId { get; set; }
        public string? ValueText { get; set; }
        public decimal? ValueNumber { get; set; }
        public bool? ValueBoolean { get; set; }
        public DateTime? ValueDate { get; set; }
        public Guid? ProductAttributeOptionId { get; set; }
        public List<Guid>? ValueOptionIds { get; set; }
    }

    public class SetProductAttributeValueValidator : AbstractValidator<SetProductAttributeValueCommand>
    {
        public SetProductAttributeValueValidator()
        {
            RuleFor(x => x.ProductAttributeId).NotEmpty().WithMessage("Attribute ID is required");
            RuleFor(x => x.OwnerId).NotEmpty().WithMessage("Owner ID is required");

            RuleFor(x => x.OwnerType)
                .Must(v => Enum.IsDefined(typeof(AttributeOwnerType), v))
                .WithMessage("OwnerType must be a valid owner type");
        }
    }

    public class SetProductAttributeValueHandler : IRequestHandler<SetProductAttributeValueCommand, Unit>
    {
        private readonly IProductUnitOfWork _uow;

        public SetProductAttributeValueHandler(IProductUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<Unit> Handle(SetProductAttributeValueCommand request, CancellationToken cancellationToken)
        {
            var attribute = await _uow.Attributes.GetByIdWithOptionsAsync(
                request.ProductAttributeId, cancellationToken);

            if (attribute == null)
                throw new KeyNotFoundException($"Attribute with ID {request.ProductAttributeId} not found");

            var ownerType = (AttributeOwnerType)request.OwnerType;

            bool ownerExists = ownerType == AttributeOwnerType.Product
                ? await _uow.Products.ExistsAsync(request.OwnerId, cancellationToken)
                : await _uow.Variants.GetByIdAsync(request.OwnerId, cancellationToken) != null;

            if (!ownerExists)
                throw new KeyNotFoundException($"{ownerType} with ID {request.OwnerId} not found");

            // Resolve the owning Product's Id for the search-reindex signal
            var productId = ownerType == AttributeOwnerType.Product
                ? request.OwnerId
                : await _uow.AttributeValues.GetProductIdForVariantAsync(request.OwnerId, cancellationToken);

            // Exactly one value field must be populated, and it must be the one matching the
            // attribute's DataType.
            string? valueText = null;
            decimal? valueNumber = null;
            bool? valueBoolean = null;
            DateTime? valueDate = null;
            Guid? optionId = null;
            string? optionIdsJson = null;

            switch (attribute.DataType)
            {
                case AttributeDataType.Text:
                    if (string.IsNullOrWhiteSpace(request.ValueText))
                        throw new ArgumentException("ValueText is required for a Text attribute");
                    valueText = request.ValueText;
                    break;

                case AttributeDataType.Number:
                    if (!request.ValueNumber.HasValue)
                        throw new ArgumentException("ValueNumber is required for a Number attribute");
                    valueNumber = request.ValueNumber;
                    break;

                case AttributeDataType.Boolean:
                    if (!request.ValueBoolean.HasValue)
                        throw new ArgumentException("ValueBoolean is required for a Boolean attribute");
                    valueBoolean = request.ValueBoolean;
                    break;

                case AttributeDataType.Date:
                    if (!request.ValueDate.HasValue)
                        throw new ArgumentException("ValueDate is required for a Date attribute");
                    valueDate = request.ValueDate;
                    break;

                case AttributeDataType.SingleSelect:
                    if (!request.ProductAttributeOptionId.HasValue)
                        throw new ArgumentException("ProductAttributeOptionId is required for a SingleSelect attribute");

                    if (!attribute.Options.Any(o => o.Id == request.ProductAttributeOptionId.Value))
                        throw new ArgumentException($"Option {request.ProductAttributeOptionId} does not belong to attribute {attribute.Code}");

                    optionId = request.ProductAttributeOptionId;
                    break;

                case AttributeDataType.MultiSelect:
                    if (request.ValueOptionIds == null || request.ValueOptionIds.Count == 0)
                        throw new ArgumentException("ValueOptionIds is required for a MultiSelect attribute");

                    var invalidOptionIds = request.ValueOptionIds.Where(id => !attribute.Options.Any(o => o.Id == id)).ToList();
                    if (invalidOptionIds.Count > 0)
                        throw new ArgumentException($"Option(s) {string.Join(", ", invalidOptionIds)} do not belong to attribute {attribute.Code}");

                    optionIdsJson = JsonConvert.SerializeObject(request.ValueOptionIds);
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported attribute data type: {attribute.DataType}");
            }

            // Upsert — mirror Product.SetTranslation's exact find-or-create shape.
            var existing = await _uow.AttributeValues.GetExistingAsync(
                ownerType, request.OwnerId, request.ProductAttributeId, cancellationToken);

            if (existing != null)
            {
                existing.ValueText = valueText;
                existing.ValueNumber = valueNumber;
                existing.ValueBoolean = valueBoolean;
                existing.ValueDate = valueDate;
                existing.ProductAttributeOptionId = optionId;
                existing.ValueOptionIdsJson = optionIdsJson;
                existing.UpdatedAt = DateTime.UtcNow;
                existing.RequestSearchReindex(productId);
            }
            else
            {
                var value = ProductAttributeValue.Create(request.ProductAttributeId, ownerType, request.OwnerId);
                value.ValueText = valueText;
                value.ValueNumber = valueNumber;
                value.ValueBoolean = valueBoolean;
                value.ValueDate = valueDate;
                value.ProductAttributeOptionId = optionId;
                value.ValueOptionIdsJson = optionIdsJson;
                value.RequestSearchReindex(productId);

                await _uow.AttributeValues.AddAsync(value, cancellationToken);
            }

            await _uow.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}
