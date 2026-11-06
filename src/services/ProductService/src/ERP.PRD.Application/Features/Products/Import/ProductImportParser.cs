using System.Globalization;
using System.Text.RegularExpressions;
using ERP.PRD.Domain.Enums;
using ERP.Shared.Streaming;

namespace ERP.PRD.Application.Features.Products.Import
{
    /// <summary>One valid row of the product CSV (codes are upper case, the numbers are parsed).</summary>
    public sealed record ProductImportData(
        int Line, string Sku, string Name, string? Description, ProductType ProductType,
        string? CategoryCode, string? BrandCode,
        decimal? Price, decimal? DiscountedPrice, decimal? Cost, string? Barcode, decimal? WeightKg, bool Active)
    {
        /// <summary>A default variant (same SKU as the product) carries the price, cost, barcode and weight; a product to publish must have one.</summary>
        public bool NeedsVariant => Active || Price.HasValue || Cost.HasValue || Barcode != null || WeightKg.HasValue;
    }

    public sealed record ProductImportParseResult(ProductImportData? Data, string? Error, string? Sku);

    /// <summary>
    /// The row rules of the product import, written out here (no MediatR validation behaviour exists in this service, so FluentValidation validators do
    /// not run on their own). Column names are the normalised header names: sku, name, description, producttype, categorycode, brandcode, price,
    /// discountedprice, cost, barcode, weightkg, status. A row's problem is returned as text, never thrown.
    /// </summary>
    public static class ProductImportParser
    {
        public static readonly string[] RequiredColumns = { "sku", "name" };

        public const string TemplateCsv =
            "sku,name,description,productType,categoryCode,brandCode,price,discountedPrice,cost,barcode,weightKg,status\r\n" +
            "TSHIRT-RED-M,Red T-shirt (M),Cotton T-shirt,Physical,APPAREL,ACME,19.90,17.50,8.20,8930000000011,0.25,Active\r\n" +
            "EBOOK-ERP-01,ERP handbook (PDF),,Digital,BOOKS,,9.99,,,,,Draft\r\n";

        private static readonly Regex SkuPattern = new("^[A-Z0-9-]{1,50}$", RegexOptions.Compiled);

        public static ProductImportParseResult Parse(CsvImportRow row)
        {
            var rawSku = row.Get("sku");
            var sku = rawSku.ToUpperInvariant();

            if (sku.Length == 0) return new(null, "The SKU is required.", null);
            if (!SkuPattern.IsMatch(sku)) return new(null, "The SKU may only hold letters, digits and '-', up to 50 characters.", sku);

            var name = row.Get("name");
            if (name.Length < 3 || name.Length > 255) return new(null, "The name must be 3 to 255 characters.", sku);

            var description = row.Get("description");
            if (description.Length > 2000) return new(null, "The description is longer than 2000 characters.", sku);

            var typeText = row.Get("producttype");
            var type = ProductType.Physical;
            if (typeText.Length > 0 && (!Enum.TryParse(typeText, ignoreCase: true, out type) || !Enum.IsDefined(type) || int.TryParse(typeText, out _)))
                return new(null, $"Unknown product type '{typeText}' (use Physical, Digital, Service or Bundle).", sku);

            var categoryCode = row.Get("categorycode").ToUpperInvariant();
            var brandCode = row.Get("brandcode").ToUpperInvariant();
            if (categoryCode.Length > 50 || brandCode.Length > 50) return new(null, "A category or brand code is longer than 50 characters.", sku);

            if (!TryDecimal(row.Get("price"), 0, 1_000_000, out var price, out var priceError)) return new(null, $"Price: {priceError}", sku);
            if (!TryDecimal(row.Get("discountedprice"), 0, 1_000_000, out var discounted, out var discountError)) return new(null, $"Discounted price: {discountError}", sku);
            if (discounted.HasValue && !price.HasValue) return new(null, "A discounted price needs a price.", sku);
            if (discounted.HasValue && price.HasValue && discounted >= price) return new(null, "The discounted price must be less than the price.", sku);
            if (!TryDecimal(row.Get("cost"), 0, 1_000_000_000, out var cost, out var costError)) return new(null, $"Cost: {costError}", sku);
            if (!TryDecimal(row.Get("weightkg"), 0, 1_000_000, out var weight, out var weightError)) return new(null, $"Weight: {weightError}", sku);

            var barcode = row.Get("barcode");
            if (barcode.Length > 50) return new(null, "The barcode is longer than 50 characters.", sku);

            var statusText = row.Get("status");
            bool active;
            if (statusText.Length == 0 || statusText.Equals("Draft", StringComparison.OrdinalIgnoreCase)) active = false;
            else if (statusText.Equals("Active", StringComparison.OrdinalIgnoreCase)) active = true;
            else return new(null, $"Unknown status '{statusText}' (use Draft or Active).", sku);

            return new(new ProductImportData(
                row.Line, sku, name, description.Length == 0 ? null : description, type,
                categoryCode.Length == 0 ? null : categoryCode, brandCode.Length == 0 ? null : brandCode,
                price, discounted, cost, barcode.Length == 0 ? null : barcode, weight, active), null, sku);
        }

        /// <summary>Blank = no value. A decimal comma ("19,90") is accepted when the cell has no dot.</summary>
        private static bool TryDecimal(string text, decimal min, decimal maxExclusive, out decimal? value, out string error)
        {
            value = null;
            error = string.Empty;
            if (text.Length == 0) return true;

            var normalised = !text.Contains('.') && text.Count(c => c == ',') == 1 ? text.Replace(',', '.') : text;
            if (!decimal.TryParse(normalised, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            {
                error = $"'{text}' is not a number.";
                return false;
            }
            if (parsed < min || parsed >= maxExclusive)
            {
                error = $"must be at least {min} and below {maxExclusive:N0}.";
                return false;
            }
            value = parsed;
            return true;
        }
    }
}
