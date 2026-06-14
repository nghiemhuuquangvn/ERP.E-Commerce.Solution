using System.Net;
using System.Net.Http.Json;
using Xunit;
using ERP.PRD.Application.Features.Products.Commands;
using ERP.PRD.Application.Features.Variants.Commands;
using ERP.PRD.Application.Features.Pricing.Commands;
using ERP.PRD.Application.Features.Pricing.Queries;

namespace ProductService.IntegrationTests
{
    public class PricingManagementTests : IClassFixture<ProductServiceWebFactory>
    {
        private readonly HttpClient _client;

        public PricingManagementTests(ProductServiceWebFactory factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task SetVariantPrice_WithValidData_ReturnsNoContent()
        {
            // Arrange - Create product and variant
            var productCmd = new CreateProductCommand
            {
                Name = "Laptop",
                Sku = "LAPTOP-001",
                ProductType = 1
            };

            var productResp = await _client.PostAsJsonAsync("/api/products", productCmd);
            var productData = await productResp.Content.ReadAsAsync<CreateProductResponse>();
            var productId = productData.ProductId;

            var variantCmd = new CreateProductVariantCommand
            {
                ProductId = productId,
                Sku = "LAPTOP-001-BLK",
                Cost = 500.00m
            };

            var variantResp = await _client.PostAsJsonAsync($"/api/products/{productId}/variants", variantCmd);
            var variantData = await variantResp.Content.ReadAsAsync<CreateProductVariantResponse>();
            var variantId = variantData.VariantId;

            var priceCmd = new SetProductPriceCommand
            {
                VariantId = variantId,
                BasePrice = 899.99m,
                DiscountedPrice = 799.99m
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/pricing/variants", priceCmd);

            // Assert
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        [Fact]
        public async Task SetVariantPrice_WithInvalidVariant_ReturnsNotFound()
        {
            // Arrange
            var invalidVariantId = Guid.NewGuid();
            var command = new SetProductPriceCommand
            {
                VariantId = invalidVariantId,
                BasePrice = 100.00m
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/pricing/variants", command);

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task GetProductPrices_ReturnsAllVariantPrices()
        {
            // Arrange - Create product with multiple variants
            var productCmd = new CreateProductCommand
            {
                Name = "Monitor",
                Sku = "MONITOR-001",
                ProductType = 1
            };

            var productResp = await _client.PostAsJsonAsync("/api/products", productCmd);
            var productData = await productResp.Content.ReadAsAsync<CreateProductResponse>();
            var productId = productData.ProductId;

            var variants = new[]
            {
                new CreateProductVariantCommand { ProductId = productId, Sku = "MONITOR-001-24", Cost = 200.00m },
                new CreateProductVariantCommand { ProductId = productId, Sku = "MONITOR-001-27", Cost = 300.00m }
            };

            foreach (var variant in variants)
            {
                await _client.PostAsJsonAsync($"/api/products/{productId}/variants", variant);
            }

            // Act
            var response = await _client.GetAsync($"/api/pricing/products/{productId}");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadAsAsync<List<VariantPriceDto>>();
            Assert.Equal(2, result.Count);
            Assert.Contains(result, p => p.Sku == "MONITOR-001-24");
            Assert.Contains(result, p => p.Sku == "MONITOR-001-27");
        }

        [Fact]
        public async Task CreatePriceList_WithValidData_ReturnsCreated()
        {
            // Arrange - Create product with variants
            var productCmd = new CreateProductCommand
            {
                Name = "Headphones",
                Sku = "HEADPHONES-001",
                ProductType = 1
            };

            var productResp = await _client.PostAsJsonAsync("/api/products", productCmd);
            var productData = await productResp.Content.ReadAsAsync<CreateProductResponse>();
            var productId = productData.ProductId;

            var variantCmd1 = new CreateProductVariantCommand
            {
                ProductId = productId,
                Sku = "HEADPHONES-001-BLK",
                Cost = 50.00m
            };

            var variantResp1 = await _client.PostAsJsonAsync($"/api/products/{productId}/variants", variantCmd1);
            var variantData1 = await variantResp1.Content.ReadAsAsync<CreateProductVariantResponse>();
            var variantId1 = variantData1.VariantId;

            var variantCmd2 = new CreateProductVariantCommand
            {
                ProductId = productId,
                Sku = "HEADPHONES-001-WHT",
                Cost = 50.00m
            };

            var variantResp2 = await _client.PostAsJsonAsync($"/api/products/{productId}/variants", variantCmd2);
            var variantData2 = await variantResp2.Content.ReadAsAsync<CreateProductVariantResponse>();
            var variantId2 = variantData2.VariantId;

            var priceListCmd = new CreatePriceListCommand
            {
                Name = "Summer Sale 2026",
                EffectiveDate = DateTime.UtcNow.AddDays(1),
                ExpiryDate = DateTime.UtcNow.AddDays(30),
                Items = new()
                {
                    new PriceListItemInput { VariantId = variantId1, BasePrice = 89.99m, DiscountedPrice = 69.99m },
                    new PriceListItemInput { VariantId = variantId2, BasePrice = 89.99m, DiscountedPrice = 74.99m }
                }
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/pricing/lists", priceListCmd);

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var result = await response.Content.ReadAsAsync<CreatePriceListResponse>();
            Assert.NotEqual(Guid.Empty, result.PriceListId);
            Assert.Equal("Summer Sale 2026", result.Name);
        }

        [Fact]
        public async Task GetPriceList_ReturnsCompleteDetails()
        {
            // Arrange - Create price list
            var productCmd = new CreateProductCommand { Name = "Keyboard", Sku = "KB-001", ProductType = 1 };
            var productResp = await _client.PostAsJsonAsync("/api/products", productCmd);
            var productData = await productResp.Content.ReadAsAsync<CreateProductResponse>();
            var productId = productData.ProductId;

            var variantCmd = new CreateProductVariantCommand { ProductId = productId, Sku = "KB-001-RGB", Cost = 30.00m };
            var variantResp = await _client.PostAsJsonAsync($"/api/products/{productId}/variants", variantCmd);
            var variantData = await variantResp.Content.ReadAsAsync<CreateProductVariantResponse>();
            var variantId = variantData.VariantId;

            var priceListCmd = new CreatePriceListCommand
            {
                Name = "Q4 2026 Pricing",
                EffectiveDate = DateTime.UtcNow,
                Items = new() { new PriceListItemInput { VariantId = variantId, BasePrice = 79.99m } }
            };

            var createResp = await _client.PostAsJsonAsync("/api/pricing/lists", priceListCmd);
            var createData = await createResp.Content.ReadAsAsync<CreatePriceListResponse>();
            var priceListId = createData.PriceListId;

            // Act
            var response = await _client.GetAsync($"/api/pricing/lists/{priceListId}");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadAsAsync<PriceListDetailDto>();
            Assert.Equal("Q4 2026 Pricing", result.Name);
            Assert.Single(result.Items);
            Assert.Equal(79.99m, result.Items[0].BasePrice);
            Assert.True(result.IsActive);
        }

        [Fact]
        public async Task CreatePriceList_WithDiscountedPrice_EffectivePriceIsDiscounted()
        {
            // Arrange
            var productCmd = new CreateProductCommand { Name = "Mouse", Sku = "MOUSE-001", ProductType = 1 };
            var productResp = await _client.PostAsJsonAsync("/api/products", productCmd);
            var productData = await productResp.Content.ReadAsAsync<CreateProductResponse>();
            var productId = productData.ProductId;

            var variantCmd = new CreateProductVariantCommand { ProductId = productId, Sku = "MOUSE-001-BLK", Cost = 15.00m };
            var variantResp = await _client.PostAsJsonAsync($"/api/products/{productId}/variants", variantCmd);
            var variantData = await variantResp.Content.ReadAsAsync<CreateProductVariantResponse>();
            var variantId = variantData.VariantId;

            var priceListCmd = new CreatePriceListCommand
            {
                Name = "Black Friday",
                EffectiveDate = DateTime.UtcNow,
                Items = new() { new PriceListItemInput { VariantId = variantId, BasePrice = 49.99m, DiscountedPrice = 29.99m } }
            };

            var createResp = await _client.PostAsJsonAsync("/api/pricing/lists", priceListCmd);
            var createData = await createResp.Content.ReadAsAsync<CreatePriceListResponse>();
            var priceListId = createData.PriceListId;

            // Act
            var response = await _client.GetAsync($"/api/pricing/lists/{priceListId}");
            var result = await response.Content.ReadAsAsync<PriceListDetailDto>();

            // Assert
            Assert.Equal(49.99m, result.Items[0].BasePrice);
            Assert.Equal(29.99m, result.Items[0].DiscountedPrice);
            Assert.Equal(29.99m, result.Items[0].EffectivePrice);
        }

        [Fact]
        public async Task CreatePriceList_DiscountMustBeLessThanBase_ReturnsBadRequest()
        {
            // Arrange
            var productCmd = new CreateProductCommand { Name = "USB Cable", Sku = "USB-001", ProductType = 1 };
            var productResp = await _client.PostAsJsonAsync("/api/products", productCmd);
            var productData = await productResp.Content.ReadAsAsync<CreateProductResponse>();
            var productId = productData.ProductId;

            var variantCmd = new CreateProductVariantCommand { ProductId = productId, Sku = "USB-001", Cost = 5.00m };
            var variantResp = await _client.PostAsJsonAsync($"/api/products/{productId}/variants", variantCmd);
            var variantData = await variantResp.Content.ReadAsAsync<CreateProductVariantResponse>();
            var variantId = variantData.VariantId;

            var priceListCmd = new CreatePriceListCommand
            {
                Name = "Invalid",
                EffectiveDate = DateTime.UtcNow,
                Items = new() { new PriceListItemInput { VariantId = variantId, BasePrice = 10.00m, DiscountedPrice = 15.00m } }
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/pricing/lists", priceListCmd);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
