using System.Net;
using System.Net.Http.Json;
using Xunit;
using ERP.PRD.Application.Features.Products.Commands;
using ERP.PRD.Application.Features.Variants.Commands;
using ERP.PRD.Application.Features.Variants.Queries;

namespace ProductService.IntegrationTests
{
    public class VariantManagementTests : IClassFixture<ProductServiceWebFactory>
    {
        private readonly HttpClient _client;

        public VariantManagementTests(ProductServiceWebFactory factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task CreateVariant_WithValidData_ReturnsCreated()
        {
            // Arrange
            var productCommand = new CreateProductCommand
            {
                Name = "T-Shirt",
                Sku = "TSHIRT-001",
                ProductType = 1,
                Description = "Classic cotton t-shirt"
            };

            var productResponse = await _client.PostAsJsonAsync("/api/products", productCommand);
            Assert.Equal(HttpStatusCode.Created, productResponse.StatusCode);

            var productData = await productResponse.Content.ReadAsAsync<CreateProductResponse>();
            var productId = productData.ProductId;

            var variantCommand = new CreateProductVariantCommand
            {
                ProductId = productId,
                Sku = "TSHIRT-001-BLK-M",
                Barcode = "1234567890123",
                Cost = 8.50m,
                Weight = 0.25m,
                Dimensions = "30x40x5"
            };

            // Act
            var response = await _client.PostAsJsonAsync($"/api/products/{productId}/variants", variantCommand);

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var result = await response.Content.ReadAsAsync<CreateProductVariantResponse>();
            Assert.NotEqual(Guid.Empty, result.VariantId);
            Assert.Equal("TSHIRT-001-BLK-M", result.Sku);
            Assert.Equal(8.50m, result.Cost);
        }

        [Fact]
        public async Task CreateVariant_WithDuplicateSku_ReturnsBadRequest()
        {
            // Arrange - Create product
            var productCommand = new CreateProductCommand
            {
                Name = "Jeans",
                Sku = "JEANS-001",
                ProductType = 1
            };

            var productResponse = await _client.PostAsJsonAsync("/api/products", productCommand);
            var productData = await productResponse.Content.ReadAsAsync<CreateProductResponse>();
            var productId = productData.ProductId;

            // Create first variant
            var variant1 = new CreateProductVariantCommand
            {
                ProductId = productId,
                Sku = "JEANS-001-BLU-32",
                Cost = 45.00m
            };

            await _client.PostAsJsonAsync($"/api/products/{productId}/variants", variant1);

            // Try to create duplicate
            var variant2 = new CreateProductVariantCommand
            {
                ProductId = productId,
                Sku = "JEANS-001-BLU-32", // Duplicate SKU
                Cost = 50.00m
            };

            // Act
            var response = await _client.PostAsJsonAsync($"/api/products/{productId}/variants", variant2);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task GetVariants_ReturnsAllVariants()
        {
            // Arrange - Create product with 3 variants
            var productCommand = new CreateProductCommand
            {
                Name = "Shoes",
                Sku = "SHOES-001",
                ProductType = 1
            };

            var productResponse = await _client.PostAsJsonAsync("/api/products", productCommand);
            var productData = await productResponse.Content.ReadAsAsync<CreateProductResponse>();
            var productId = productData.ProductId;

            var variants = new[]
            {
                new CreateProductVariantCommand { ProductId = productId, Sku = "SHOES-001-RED-8", Cost = 75.00m },
                new CreateProductVariantCommand { ProductId = productId, Sku = "SHOES-001-BLU-8", Cost = 75.00m },
                new CreateProductVariantCommand { ProductId = productId, Sku = "SHOES-001-BLK-8", Cost = 75.00m }
            };

            foreach (var variant in variants)
            {
                await _client.PostAsJsonAsync($"/api/products/{productId}/variants", variant);
            }

            // Act
            var response = await _client.GetAsync($"/api/products/{productId}/variants");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadAsAsync<List<ProductVariantDto>>();
            Assert.Equal(3, result.Count);
            Assert.Contains(result, v => v.Sku == "SHOES-001-RED-8");
            Assert.Contains(result, v => v.Sku == "SHOES-001-BLU-8");
            Assert.Contains(result, v => v.Sku == "SHOES-001-BLK-8");
        }

        [Fact]
        public async Task UpdateVariant_WithValidData_ReturnsNoContent()
        {
            // Arrange
            var productCommand = new CreateProductCommand
            {
                Name = "Hat",
                Sku = "HAT-001",
                ProductType = 1
            };

            var productResponse = await _client.PostAsJsonAsync("/api/products", productCommand);
            var productData = await productResponse.Content.ReadAsAsync<CreateProductResponse>();
            var productId = productData.ProductId;

            var variantCommand = new CreateProductVariantCommand
            {
                ProductId = productId,
                Sku = "HAT-001-BLK-OS",
                Cost = 15.00m,
                Weight = 0.1m
            };

            var variantResponse = await _client.PostAsJsonAsync($"/api/products/{productId}/variants", variantCommand);
            var variantData = await variantResponse.Content.ReadAsAsync<CreateProductVariantResponse>();
            var variantId = variantData.VariantId;

            var updateCommand = new UpdateProductVariantCommand
            {
                ProductId = productId,
                VariantId = variantId,
                Cost = 18.50m, // Price increase
                Barcode = "NEW-BARCODE-123"
            };

            // Act
            var response = await _client.PutAsJsonAsync($"/api/products/{productId}/variants/{variantId}", updateCommand);

            // Assert
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

            // Verify the update
            var getResponse = await _client.GetAsync($"/api/products/{productId}/variants/{variantId}");
            var updated = await getResponse.Content.ReadAsAsync<ProductVariantDto>();
            Assert.Equal(18.50m, updated.Cost);
            Assert.Equal("NEW-BARCODE-123", updated.Barcode);
        }

        [Fact]
        public async Task DeleteVariant_WithMultipleVariants_ReturnsNoContent()
        {
            // Arrange - Create product with 2 variants
            var productCommand = new CreateProductCommand
            {
                Name = "Gloves",
                Sku = "GLOVES-001",
                ProductType = 1
            };

            var productResponse = await _client.PostAsJsonAsync("/api/products", productCommand);
            var productData = await productResponse.Content.ReadAsAsync<CreateProductResponse>();
            var productId = productData.ProductId;

            var variant1 = new CreateProductVariantCommand { ProductId = productId, Sku = "GLOVES-001-S", Cost = 12.00m };
            var variant2 = new CreateProductVariantCommand { ProductId = productId, Sku = "GLOVES-001-L", Cost = 12.00m };

            var resp1 = await _client.PostAsJsonAsync($"/api/products/{productId}/variants", variant1);
            var data1 = await resp1.Content.ReadAsAsync<CreateProductVariantResponse>();
            var variantId1 = data1.VariantId;

            await _client.PostAsJsonAsync($"/api/products/{productId}/variants", variant2);

            // Act - Delete first variant
            var response = await _client.DeleteAsync($"/api/products/{productId}/variants/{variantId1}");

            // Assert
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

            // Verify it's deleted
            var getResponse = await _client.GetAsync($"/api/products/{productId}/variants");
            var remaining = await getResponse.Content.ReadAsAsync<List<ProductVariantDto>>();
            Assert.Single(remaining);
            Assert.DoesNotContain(remaining, v => v.Id == variantId1);
        }

        [Fact]
        public async Task DeleteVariant_WithOnlyOneVariant_ReturnsBadRequest()
        {
            // Arrange - Create product with only 1 variant
            var productCommand = new CreateProductCommand
            {
                Name = "Scarf",
                Sku = "SCARF-001",
                ProductType = 1
            };

            var productResponse = await _client.PostAsJsonAsync("/api/products", productCommand);
            var productData = await productResponse.Content.ReadAsAsync<CreateProductResponse>();
            var productId = productData.ProductId;

            var variantCommand = new CreateProductVariantCommand
            {
                ProductId = productId,
                Sku = "SCARF-001-RED",
                Cost = 25.00m
            };

            var variantResponse = await _client.PostAsJsonAsync($"/api/products/{productId}/variants", variantCommand);
            var variantData = await variantResponse.Content.ReadAsAsync<CreateProductVariantResponse>();
            var variantId = variantData.VariantId;

            // Act - Try to delete the only variant
            var response = await _client.DeleteAsync($"/api/products/{productId}/variants/{variantId}");

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task GetVariantBySku_WithValidSku_ReturnsVariant()
        {
            // Arrange - Create product and variant
            var productCommand = new CreateProductCommand
            {
                Name = "Belt",
                Sku = "BELT-001",
                ProductType = 1
            };

            var productResponse = await _client.PostAsJsonAsync("/api/products", productCommand);
            var productData = await productResponse.Content.ReadAsAsync<CreateProductResponse>();
            var productId = productData.ProductId;

            var variantCommand = new CreateProductVariantCommand
            {
                ProductId = productId,
                Sku = "BELT-001-BLK-M",
                Cost = 30.00m
            };

            await _client.PostAsJsonAsync($"/api/products/{productId}/variants", variantCommand);

            // Act - Lookup by SKU (global, not product-specific)
            var response = await _client.GetAsync("/api/products/sku/BELT-001-BLK-M");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadAsAsync<ProductVariantDto>();
            Assert.Equal("BELT-001-BLK-M", result.Sku);
            Assert.Equal(productId, result.ProductId);
            Assert.Equal(30.00m, result.Cost);
        }

        [Fact]
        public async Task GetVariantBySku_WithInvalidSku_ReturnsNotFound()
        {
            // Act
            var response = await _client.GetAsync("/api/products/sku/NONEXISTENT-SKU");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
