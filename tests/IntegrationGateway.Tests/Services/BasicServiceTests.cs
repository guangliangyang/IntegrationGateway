using IntegrationGateway.Models.DTOs;
using IntegrationGateway.Models.Common;
using IntegrationGateway.Services.Implementation;
using IntegrationGateway.Services.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using FluentAssertions;

namespace IntegrationGateway.Tests.Services;

public class BasicServiceTests
{
    [Fact]
    public void ProductDto_Should_Have_Required_Properties()
    {
        // Arrange & Act
        var product = new ProductDto
        {
            Id = "prod-001",
            Name = "Test Product",
            Description = "Test Description",
            Price = 99.99m,
            Category = "Electronics",
            IsActive = true,
            StockQuantity = 50,
            InStock = true,
            WarehouseLocation = "Warehouse-A-01"
        };

        // Assert
        product.Id.Should().Be("prod-001");
        product.Name.Should().Be("Test Product");
        product.Description.Should().Be("Test Description");
        product.Price.Should().Be(99.99m);
        product.Category.Should().Be("Electronics");
        product.IsActive.Should().BeTrue();
        product.StockQuantity.Should().Be(50);
        product.InStock.Should().BeTrue();
        product.WarehouseLocation.Should().Be("Warehouse-A-01");
    }

    [Fact]
    public void ProductV2Dto_Should_Extend_ProductDto_With_Additional_Properties()
    {
        // Arrange & Act
        var productV2 = new ProductV2Dto
        {
            Id = "prod-002",
            Name = "Test Product V2",
            Price = 149.99m,
            Category = "Electronics",
            Supplier = "Test Supplier",
            Tags = new List<string> { "tag1", "tag2" },
            Metadata = new Dictionary<string, object> { { "key1", "value1" } }
        };

        // Assert
        productV2.Id.Should().Be("prod-002");
        productV2.Name.Should().Be("Test Product V2");
        productV2.Supplier.Should().Be("Test Supplier");
        productV2.Tags.Should().HaveCount(2);
        productV2.Tags.Should().Contain("tag1");
        productV2.Metadata.Should().ContainKey("key1");
    }

    [Fact]
    public void CreateProductRequest_Should_Validate_Required_Fields()
    {
        // Arrange & Act
        var request = new CreateProductRequest
        {
            Name = "New Product",
            Description = "New Description",
            Price = 99.99m,
            Category = "Electronics",
            IsActive = true
        };

        // Assert
        request.Name.Should().Be("New Product");
        request.Description.Should().Be("New Description");
        request.Price.Should().Be(99.99m);
        request.Category.Should().Be("Electronics");
        request.IsActive.Should().BeTrue();
    }

    [Fact]
    public void UpdateProductRequest_Should_Allow_Partial_Updates()
    {
        // Arrange & Act
        var request = new UpdateProductRequest
        {
            Name = "Updated Name",
            Price = 199.99m
            // Description, Category, and IsActive are intentionally null/not set
        };

        // Assert
        request.Name.Should().Be("Updated Name");
        request.Price.Should().Be(199.99m);
        request.Description.Should().BeNull();
        request.Category.Should().BeNull();
        request.IsActive.Should().BeNull();
    }

    [Fact]
    public void ProductListResponse_Should_Initialize_With_Default_Values()
    {
        // Arrange & Act
        var response = new ProductListResponse();

        // Assert
        response.Products.Should().NotBeNull();
        response.Products.Should().BeEmpty();
        response.Total.Should().Be(0);
        response.Page.Should().Be(1);
        response.PageSize.Should().Be(50);
    }

    [Fact]
    public void ProductListV2Response_Should_Include_Metadata()
    {
        // Arrange & Act
        var response = new ProductListV2Response
        {
            Products = new List<ProductV2Dto>(),
            Total = 100,
            Page = 2,
            PageSize = 25,
            Metadata = new Dictionary<string, object> { { "source", "api-v2" } }
        };

        // Assert
        response.Products.Should().NotBeNull();
        response.Total.Should().Be(100);
        response.Page.Should().Be(2);
        response.PageSize.Should().Be(25);
        response.Metadata.Should().ContainKey("source");
        response.Metadata["source"].Should().Be("api-v2");
    }

    [Fact]
    public void CacheService_Should_Initialize_With_MemoryCache()
    {
        // Arrange
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var logger = new Mock<ILogger<CacheService>>();
        var cacheOptions = new Mock<IOptions<CacheOptions>>();
        cacheOptions.Setup(x => x.Value).Returns(new CacheOptions());

        // Act
        var cacheService = new CacheService(memoryCache, logger.Object, cacheOptions.Object);

        // Assert
        cacheService.Should().NotBeNull();
    }

    [Fact]
    public async Task CacheService_Should_Store_And_Retrieve_Values()
    {
        // Arrange
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var logger = new Mock<ILogger<CacheService>>();
        var cacheOptions = new Mock<IOptions<CacheOptions>>();
        cacheOptions.Setup(x => x.Value).Returns(new CacheOptions());
        var cacheService = new CacheService(memoryCache, logger.Object, cacheOptions.Object);
        const string key = "test-key";
        const string value = "test-value";

        // Act
        await cacheService.SetAsync(key, value, TimeSpan.FromMinutes(5));
        var retrievedValue = await cacheService.GetAsync<string>(key);

        // Assert
        retrievedValue.Should().Be(value);
    }

    [Fact]
    public async Task CacheService_Should_Return_Default_For_Missing_Key()
    {
        // Arrange
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var logger = new Mock<ILogger<CacheService>>();
        var cacheOptions = new Mock<IOptions<CacheOptions>>();
        cacheOptions.Setup(x => x.Value).Returns(new CacheOptions());
        var cacheService = new CacheService(memoryCache, logger.Object, cacheOptions.Object);

        // Act
        var result = await cacheService.GetAsync<string>("non-existent-key");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CacheService_Should_Remove_Values()
    {
        // Arrange
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var logger = new Mock<ILogger<CacheService>>();
        var cacheOptions = new Mock<IOptions<CacheOptions>>();
        cacheOptions.Setup(x => x.Value).Returns(new CacheOptions());
        var cacheService = new CacheService(memoryCache, logger.Object, cacheOptions.Object);
        const string key = "test-key";
        const string value = "test-value";

        // Act
        await cacheService.SetAsync(key, value, TimeSpan.FromMinutes(5));
        var beforeRemoval = await cacheService.GetAsync<string>(key);
        await cacheService.RemoveAsync(key);
        var afterRemoval = await cacheService.GetAsync<string>(key);

        // Assert
        beforeRemoval.Should().Be(value);
        afterRemoval.Should().BeNull();
    }
}