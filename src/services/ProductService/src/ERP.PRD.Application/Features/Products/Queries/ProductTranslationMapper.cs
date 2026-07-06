namespace ERP.PRD.Application.Features.Products.Queries
{
    /// <summary>
    /// Maps a Product aggregate to its ProductDto, applying the content-translation
    /// fallback pattern: when a LanguageCode is requested and a matching translation
    /// row exists, the translated Name/Description are returned and LanguageCode on
    /// the DTO reflects the requested language; otherwise the base entity's own
    /// Name/Description (default-language fallback) are returned and LanguageCode
    /// reflects that no translation was applied.
    /// </summary>
    public static class ProductTranslationMapper
    {
        public static ProductDto MapToDto(Domain.Entities.Product product, string? languageCode)
        {
            var translation = string.IsNullOrEmpty(languageCode)
                ? null
                : product.Translations.FirstOrDefault(t => t.LanguageCode == languageCode);

            var primaryImage = product.Images.FirstOrDefault(i => i.IsPrimary)
                ?? product.Images.FirstOrDefault();

            return new ProductDto
            {
                Id = product.Id,
                Name = translation?.Name ?? product.Name,
                Sku = product.Sku,
                Description = translation?.Description ?? product.Description,
                ProductType = (int)product.ProductType,
                Status = (int)product.Status,
                CategoryId = product.CategoryId,
                CategoryName = string.Empty, // populated at query level when category join is available
                HasImage = product.HasImage,
                ImageUrl = primaryImage?.ImageUrl,
                VariantCount = product.Variants.Count,
                StockQuantity = product.StockQuantity,
                Price = 0m, // retail price is stored in PriceList; default 0 until pricing endpoint is called
                LanguageCode = translation != null ? translation.LanguageCode : "default",
                CreatedAt = product.CreatedAt,
                UpdatedAt = product.UpdatedAt
            };
        }
    }
}
