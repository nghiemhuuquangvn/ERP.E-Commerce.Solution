using System.Net;
using System.Net.Http.Json;
using Xunit;
using ERP.PRD.Application.Features.Products.Commands;
using ERP.PRD.Application.Features.Images.Commands;
using ERP.PRD.Application.Features.Images.Queries;

namespace ProductService.IntegrationTests
{
    public class ImageManagementTests : IClassFixture<ProductServiceWebFactory>
    {
        private readonly HttpClient _client;

        public ImageManagementTests(ProductServiceWebFactory factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task UploadImage_WithValidData_ReturnsCreated()
        {
            // Arrange
            var productCmd = new CreateProductCommand
            {
                Name = "Monitor",
                Sku = "MONITOR-001",
                ProductType = 1
            };

            var productResp = await _client.PostAsJsonAsync("/api/products", productCmd);
            var productData = await productResp.Content.ReadAsAsync<CreateProductResponse>();
            var productId = productData.ProductId;

            var imageCmd = new UploadProductImageCommand
            {
                ProductId = productId,
                ImageUrl = "https://example.com/monitor-front.jpg",
                AltText = "Monitor Front View",
                SetAsPrimary = true
            };

            // Act
            var response = await _client.PostAsJsonAsync($"/api/products/{productId}/images", imageCmd);

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var result = await response.Content.ReadAsAsync<UploadProductImageResponse>();
            Assert.NotEqual(Guid.Empty, result.ImageId);
            Assert.Equal("https://example.com/monitor-front.jpg", result.ImageUrl);
            Assert.True(result.IsPrimary);
        }

        [Fact]
        public async Task UploadImage_FirstImageBecomePrimary_Automatically()
        {
            // Arrange
            var productCmd = new CreateProductCommand
            {
                Name = "Keyboard",
                Sku = "KB-001",
                ProductType = 1
            };

            var productResp = await _client.PostAsJsonAsync("/api/products", productCmd);
            var productData = await productResp.Content.ReadAsAsync<CreateProductResponse>();
            var productId = productData.ProductId;

            var imageCmd = new UploadProductImageCommand
            {
                ProductId = productId,
                ImageUrl = "https://example.com/keyboard.jpg",
                AltText = "Keyboard",
                SetAsPrimary = false // Explicitly not primary
            };

            // Act
            var response = await _client.PostAsJsonAsync($"/api/products/{productId}/images", imageCmd);

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var result = await response.Content.ReadAsAsync<UploadProductImageResponse>();
            Assert.True(result.IsPrimary); // First image is always primary
        }

        [Fact]
        public async Task GetImages_ReturnsAllProductImages()
        {
            // Arrange - Create product and upload images
            var productCmd = new CreateProductCommand
            {
                Name = "Laptop",
                Sku = "LAPTOP-001",
                ProductType = 1
            };

            var productResp = await _client.PostAsJsonAsync("/api/products", productCmd);
            var productData = await productResp.Content.ReadAsAsync<CreateProductResponse>();
            var productId = productData.ProductId;

            var images = new[]
            {
                new UploadProductImageCommand { ProductId = productId, ImageUrl = "https://example.com/laptop-front.jpg", SetAsPrimary = true },
                new UploadProductImageCommand { ProductId = productId, ImageUrl = "https://example.com/laptop-side.jpg" },
                new UploadProductImageCommand { ProductId = productId, ImageUrl = "https://example.com/laptop-back.jpg" }
            };

            foreach (var img in images)
            {
                await _client.PostAsJsonAsync($"/api/products/{productId}/images", img);
            }

            // Act
            var response = await _client.GetAsync($"/api/products/{productId}/images");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadAsAsync<List<ProductImageDto>>();
            Assert.Equal(3, result.Count);
            Assert.Contains(result, i => i.IsPrimary);
            Assert.Single(result.Where(i => i.IsPrimary));
        }

        [Fact]
        public async Task RemoveImage_WithValidData_ReturnsNoContent()
        {
            // Arrange
            var productCmd = new CreateProductCommand
            {
                Name = "Mouse",
                Sku = "MOUSE-001",
                ProductType = 1
            };

            var productResp = await _client.PostAsJsonAsync("/api/products", productCmd);
            var productData = await productResp.Content.ReadAsAsync<CreateProductResponse>();
            var productId = productData.ProductId;

            var imageCmd1 = new UploadProductImageCommand
            {
                ProductId = productId,
                ImageUrl = "https://example.com/mouse-1.jpg",
                SetAsPrimary = true
            };

            var imageResp1 = await _client.PostAsJsonAsync($"/api/products/{productId}/images", imageCmd1);
            var imageData1 = await imageResp1.Content.ReadAsAsync<UploadProductImageResponse>();
            var imageId1 = imageData1.ImageId;

            var imageCmd2 = new UploadProductImageCommand
            {
                ProductId = productId,
                ImageUrl = "https://example.com/mouse-2.jpg"
            };

            await _client.PostAsJsonAsync($"/api/products/{productId}/images", imageCmd2);

            // Act
            var response = await _client.DeleteAsync($"/api/products/{productId}/images/{imageId1}");

            // Assert
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

            // Verify image was removed
            var getResp = await _client.GetAsync($"/api/products/{productId}/images");
            var images = await getResp.Content.ReadAsAsync<List<ProductImageDto>>();
            Assert.Single(images);
        }

        [Fact]
        public async Task RemoveImage_PrimaryIsRemoved_NewPrimarySelected()
        {
            // Arrange
            var productCmd = new CreateProductCommand
            {
                Name = "Headphones",
                Sku = "HP-001",
                ProductType = 1
            };

            var productResp = await _client.PostAsJsonAsync("/api/products", productCmd);
            var productData = await productResp.Content.ReadAsAsync<CreateProductResponse>();
            var productId = productData.ProductId;

            var imageCmd1 = new UploadProductImageCommand
            {
                ProductId = productId,
                ImageUrl = "https://example.com/hp-1.jpg",
                SetAsPrimary = true
            };

            var imageResp1 = await _client.PostAsJsonAsync($"/api/products/{productId}/images", imageCmd1);
            var imageData1 = await imageResp1.Content.ReadAsAsync<UploadProductImageResponse>();
            var imageId1 = imageData1.ImageId;

            var imageCmd2 = new UploadProductImageCommand
            {
                ProductId = productId,
                ImageUrl = "https://example.com/hp-2.jpg"
            };

            await _client.PostAsJsonAsync($"/api/products/{productId}/images", imageCmd2);

            // Act - Remove primary image
            await _client.DeleteAsync($"/api/products/{productId}/images/{imageId1}");

            // Assert - New primary is selected
            var getResp = await _client.GetAsync($"/api/products/{productId}/images");
            var images = await getResp.Content.ReadAsAsync<List<ProductImageDto>>();
            Assert.Single(images);
            Assert.True(images[0].IsPrimary);
        }

        [Fact]
        public async Task SetPrimaryImage_WithValidData_ReturnsNoContent()
        {
            // Arrange
            var productCmd = new CreateProductCommand
            {
                Name = "Cable",
                Sku = "CABLE-001",
                ProductType = 1
            };

            var productResp = await _client.PostAsJsonAsync("/api/products", productCmd);
            var productData = await productResp.Content.ReadAsAsync<CreateProductResponse>();
            var productId = productData.ProductId;

            var imageCmd1 = new UploadProductImageCommand
            {
                ProductId = productId,
                ImageUrl = "https://example.com/cable-1.jpg",
                SetAsPrimary = true
            };

            await _client.PostAsJsonAsync($"/api/products/{productId}/images", imageCmd1);

            var imageCmd2 = new UploadProductImageCommand
            {
                ProductId = productId,
                ImageUrl = "https://example.com/cable-2.jpg"
            };

            var imageResp2 = await _client.PostAsJsonAsync($"/api/products/{productId}/images", imageCmd2);
            var imageData2 = await imageResp2.Content.ReadAsAsync<UploadProductImageResponse>();
            var imageId2 = imageData2.ImageId;

            // Act - Set second image as primary
            var response = await _client.PostAsJsonAsync($"/api/products/{productId}/images/{imageId2}/primary", null);

            // Assert
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

            // Verify
            var getResp = await _client.GetAsync($"/api/products/{productId}/images");
            var images = await getResp.Content.ReadAsAsync<List<ProductImageDto>>();
            var primaryImage = images.FirstOrDefault(i => i.IsPrimary);
            Assert.NotNull(primaryImage);
            Assert.Equal(imageId2, primaryImage.Id);
        }

        [Fact]
        public async Task UploadImage_WithInvalidProduct_ReturnsNotFound()
        {
            // Arrange
            var invalidProductId = Guid.NewGuid();
            var imageCmd = new UploadProductImageCommand
            {
                ProductId = invalidProductId,
                ImageUrl = "https://example.com/image.jpg"
            };

            // Act
            var response = await _client.PostAsJsonAsync($"/api/products/{invalidProductId}/images", imageCmd);

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task RemoveImage_WithInvalidImage_ReturnsNotFound()
        {
            // Arrange
            var productCmd = new CreateProductCommand
            {
                Name = "Speaker",
                Sku = "SPEAKER-001",
                ProductType = 1
            };

            var productResp = await _client.PostAsJsonAsync("/api/products", productCmd);
            var productData = await productResp.Content.ReadAsAsync<CreateProductResponse>();
            var productId = productData.ProductId;

            var invalidImageId = Guid.NewGuid();

            // Act
            var response = await _client.DeleteAsync($"/api/products/{productId}/images/{invalidImageId}");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task UploadImage_WithInvalidUrl_ReturnsBadRequest()
        {
            // Arrange
            var productCmd = new CreateProductCommand
            {
                Name = "Charger",
                Sku = "CHARGER-001",
                ProductType = 1
            };

            var productResp = await _client.PostAsJsonAsync("/api/products", productCmd);
            var productData = await productResp.Content.ReadAsAsync<CreateProductResponse>();
            var productId = productData.ProductId;

            var imageCmd = new UploadProductImageCommand
            {
                ProductId = productId,
                ImageUrl = "invalid-url" // Not a valid URL
            };

            // Act
            var response = await _client.PostAsJsonAsync($"/api/products/{productId}/images", imageCmd);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
