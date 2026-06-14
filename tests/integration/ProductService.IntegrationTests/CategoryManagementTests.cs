using System.Net;
using System.Net.Http.Json;
using Xunit;
using ERP.PRD.Application.Features.Categories.Commands;
using ERP.PRD.Application.Features.Categories.Queries;

namespace ProductService.IntegrationTests
{
    public class CategoryManagementTests : IClassFixture<ProductServiceWebFactory>
    {
        private readonly HttpClient _client;

        public CategoryManagementTests(ProductServiceWebFactory factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task CreateCategory_WithValidData_ReturnsCreated()
        {
            // Arrange
            var command = new CreateCategoryCommand
            {
                Name = "Electronics"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/categories", command);

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var result = await response.Content.ReadAsAsync<CreateCategoryResponse>();
            Assert.NotEqual(Guid.Empty, result.CategoryId);
            Assert.Equal("Electronics", result.Name);
        }

        [Fact]
        public async Task CreateSubcategory_WithParentCategory_ReturnsCreated()
        {
            // Arrange - Create parent category
            var parentCommand = new CreateCategoryCommand { Name = "Clothing" };
            var parentResponse = await _client.PostAsJsonAsync("/api/categories", parentCommand);
            var parentData = await parentResponse.Content.ReadAsAsync<CreateCategoryResponse>();
            var parentId = parentData.CategoryId;

            // Create subcategory
            var childCommand = new CreateCategoryCommand
            {
                Name = "Men's Clothing",
                ParentCategoryId = parentId
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/categories", childCommand);

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var result = await response.Content.ReadAsAsync<CreateCategoryResponse>();
            Assert.NotEqual(Guid.Empty, result.CategoryId);
            Assert.Equal("Men's Clothing", result.Name);
        }

        [Fact]
        public async Task GetCategories_ReturnsRootCategories()
        {
            // Arrange
            var cmd1 = new CreateCategoryCommand { Name = "Books" };
            var cmd2 = new CreateCategoryCommand { Name = "Games" };

            await _client.PostAsJsonAsync("/api/categories", cmd1);
            await _client.PostAsJsonAsync("/api/categories", cmd2);

            // Act
            var response = await _client.GetAsync("/api/categories");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadAsAsync<List<CategoryDto>>();
            Assert.NotEmpty(result);
            Assert.Contains(result, c => c.Name == "Books");
            Assert.Contains(result, c => c.Name == "Games");
        }

        [Fact]
        public async Task GetCategories_WithParentFilter_ReturnsChildCategories()
        {
            // Arrange - Create parent and children
            var parentCmd = new CreateCategoryCommand { Name = "Fashion" };
            var parentResp = await _client.PostAsJsonAsync("/api/categories", parentCmd);
            var parentData = await parentResp.Content.ReadAsAsync<CreateCategoryResponse>();
            var parentId = parentData.CategoryId;

            var child1 = new CreateCategoryCommand { Name = "Shoes", ParentCategoryId = parentId };
            var child2 = new CreateCategoryCommand { Name = "Accessories", ParentCategoryId = parentId };

            await _client.PostAsJsonAsync("/api/categories", child1);
            await _client.PostAsJsonAsync("/api/categories", child2);

            // Act
            var response = await _client.GetAsync($"/api/categories?parentId={parentId}");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadAsAsync<List<CategoryDto>>();
            Assert.Equal(2, result.Count);
            Assert.Contains(result, c => c.Name == "Shoes");
            Assert.Contains(result, c => c.Name == "Accessories");
        }

        [Fact]
        public async Task UpdateCategory_WithValidData_ReturnsNoContent()
        {
            // Arrange
            var createCmd = new CreateCategoryCommand { Name = "Home & Garden" };
            var createResp = await _client.PostAsJsonAsync("/api/categories", createCmd);
            var createData = await createResp.Content.ReadAsAsync<CreateCategoryResponse>();
            var categoryId = createData.CategoryId;

            var updateCmd = new UpdateCategoryCommand
            {
                CategoryId = categoryId,
                Name = "Home & Garden - Updated"
            };

            // Act
            var response = await _client.PutAsJsonAsync($"/api/categories/{categoryId}", updateCmd);

            // Assert
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

            // Verify
            var getResp = await _client.GetAsync("/api/categories");
            var categories = await getResp.Content.ReadAsAsync<List<CategoryDto>>();
            Assert.Contains(categories, c => c.Name == "Home & Garden - Updated");
        }

        [Fact]
        public async Task CreateCategory_WithInvalidParent_ReturnsBadRequest()
        {
            // Arrange
            var invalidParentId = Guid.NewGuid();
            var command = new CreateCategoryCommand
            {
                Name = "Invalid Child",
                ParentCategoryId = invalidParentId
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/categories", command);

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task UpdateCategory_WithInvalidParent_ReturnsBadRequest()
        {
            // Arrange - Create category
            var createCmd = new CreateCategoryCommand { Name = "Original" };
            var createResp = await _client.PostAsJsonAsync("/api/categories", createCmd);
            var createData = await createResp.Content.ReadAsAsync<CreateCategoryResponse>();
            var categoryId = createData.CategoryId;

            // Try to update with invalid parent
            var invalidParentId = Guid.NewGuid();
            var updateCmd = new UpdateCategoryCommand
            {
                CategoryId = categoryId,
                ParentCategoryId = invalidParentId
            };

            // Act
            var response = await _client.PutAsJsonAsync($"/api/categories/{categoryId}", updateCmd);

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
