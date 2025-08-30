using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using IntegrationGateway.Services.Implementation;
using IntegrationGateway.Services.Interfaces;
using IntegrationGateway.Models.DTOs;
using IntegrationGateway.Models.External;
using IntegrationGateway.Models.Common;
using IntegrationGateway.Models.Exceptions;

namespace IntegrationGateway.Tests.UnitTests.Services;

/// <summary>
/// Unit tests for ProductService business logic and exception handling
/// </summary>
public class ProductServiceTests
{
    private readonly Mock<IErpService> _mockErpService;
    private readonly Mock<IWarehouseService> _mockWarehouseService;
    private readonly Mock<IIdempotencyService> _mockIdempotencyService;
    private readonly Mock<ILogger<ProductService>> _mockLogger;
    private readonly ProductService _productService;

    public ProductServiceTests()
    {
        _mockErpService = new Mock<IErpService>();
        _mockWarehouseService = new Mock<IWarehouseService>();
        _mockIdempotencyService = new Mock<IIdempotencyService>();
        _mockLogger = new Mock<ILogger<ProductService>>();
        
        _productService = new ProductService(
            _mockErpService.Object,
            _mockWarehouseService.Object,
            _mockIdempotencyService.Object,
            _mockLogger.Object);
    }

    #region GetProductAsync Tests

    [Fact]
    public async Task GetProductAsync_WithValidId_ShouldReturnProduct()
    {
        // Arrange
        var productId = "test-product-123";
        var erpProduct = new ErpProduct
        {
            Id = productId,
            Name = "Test Product",
            Description = "Test Description",
            Price = 29.99m,
            Category = "Test Category",
            IsActive = true
        };
        var warehouseStock = new WarehouseStock
        {
            ProductId = productId,
            Quantity = 50,
            InStock = true
        };

        _mockErpService.Setup(x => x.GetProductAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ErpResponse<ErpProduct> { Success = true, Data = erpProduct });
        
        _mockWarehouseService.Setup(x => x.GetStockAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WarehouseResponse<WarehouseStock> { Success = true, Data = warehouseStock });

        // Act
        var result = await _productService.GetProductAsync(productId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(productId);
        result.Name.Should().Be("Test Product");
        result.Price.Should().Be(29.99m);
        result.StockQuantity.Should().Be(50);
        result.InStock.Should().BeTrue();
    }

    [Fact]
    public async Task GetProductAsync_WithNullProductId_ShouldThrowValidationException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _productService.GetProductAsync(null!));
        
        exception.Message.Should().Be("Product ID cannot be null or empty");
        exception.StatusCode.Should().Be(400);
        exception.ErrorType.Should().Be("validation_error");
    }

    [Fact]
    public async Task GetProductAsync_WithEmptyProductId_ShouldThrowValidationException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _productService.GetProductAsync("   "));
        
        exception.Message.Should().Be("Product ID cannot be null or empty");
    }

    [Fact]
    public async Task GetProductAsync_WhenErpProductNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        var productId = "nonexistent-product";
        
        _mockErpService.Setup(x => x.GetProductAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ErpResponse<ErpProduct> { Success = true, Data = null });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _productService.GetProductAsync(productId));
        
        exception.Message.Should().Be($"Entity \"Product\" ({productId}) was not found.");
        exception.StatusCode.Should().Be(404);
        exception.ErrorType.Should().Be("not_found");
    }

    [Fact]
    public async Task GetProductAsync_WhenErpServiceFails_ShouldThrowExternalServiceException()
    {
        // Arrange
        var productId = "test-product";
        
        _mockErpService.Setup(x => x.GetProductAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ErpResponse<ErpProduct> { Success = false, ErrorMessage = "Database connection failed" });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ExternalServiceException>(
            () => _productService.GetProductAsync(productId));
        
        exception.StatusCode.Should().Be(502);
        exception.ErrorType.Should().Be("external_service_error");
        exception.ServiceName.Should().Be("ERP");
    }

    #endregion

    #region GetProductsAsync Tests

    [Fact]
    public async Task GetProductsAsync_WithValidParameters_ShouldReturnProducts()
    {
        // Arrange
        var erpProducts = new List<ErpProduct>
        {
            new ErpProduct { Id = "1", Name = "Product 1", Price = 10.99m, Category = "Cat1", IsActive = true },
            new ErpProduct { Id = "2", Name = "Product 2", Price = 20.99m, Category = "Cat2", IsActive = true },
            new ErpProduct { Id = "3", Name = "Product 3", Price = 30.99m, Category = "Cat3", IsActive = false }
        };

        var warehouseStocks = new Dictionary<string, WarehouseStock>
        {
            { "1", new WarehouseStock { ProductId = "1", Quantity = 10, InStock = true } },
            { "2", new WarehouseStock { ProductId = "2", Quantity = 0, InStock = false } },
            { "3", new WarehouseStock { ProductId = "3", Quantity = 5, InStock = true } }
        };

        _mockErpService.Setup(x => x.GetProductsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ErpResponse<List<ErpProduct>> { Success = true, Data = erpProducts });

        _mockWarehouseService.Setup(x => x.GetBulkStockAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WarehouseResponse<BulkStockResponse> 
            { 
                Success = true, 
                Data = new BulkStockResponse { Stocks = warehouseStocks.Values.ToList() } 
            });

        // Act
        var result = await _productService.GetProductsAsync(1, 50);

        // Assert
        result.Should().NotBeNull();
        result.Products.Should().HaveCount(3);
        result.Total.Should().Be(3);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(50);
        
        // Verify first product mapping
        var firstProduct = result.Products.First();
        firstProduct.Id.Should().Be("1");
        firstProduct.StockQuantity.Should().Be(10);
        firstProduct.InStock.Should().BeTrue();
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(-1, 50)] 
    [InlineData(1, 0)]
    [InlineData(1, -10)]
    public async Task GetProductsAsync_WithInvalidPagination_ShouldThrowValidationException(int page, int pageSize)
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _productService.GetProductsAsync(page, pageSize));
        
        exception.StatusCode.Should().Be(400);
        exception.ErrorType.Should().Be("validation_error");
    }

    [Fact]
    public async Task GetProductsAsync_WithLargePagination_ShouldThrowValidationException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _productService.GetProductsAsync(1, 1001));
        
        exception.Message.Should().Contain("Page size cannot exceed 1000");
    }

    #endregion

    #region UpdateProductAsync Tests

    [Fact]
    public async Task UpdateProductAsync_WithValidRequest_ShouldReturnUpdatedProduct()
    {
        // Arrange
        var productId = "test-product-123";
        var updateRequest = new UpdateProductRequest
        {
            Name = "Updated Product",
            Description = "Updated Description",
            Price = 39.99m,
            Category = "Updated Category",
            IsActive = false
        };

        var updatedErpProduct = new ErpProduct
        {
            Id = productId,
            Name = updateRequest.Name,
            Description = updateRequest.Description,
            Price = updateRequest.Price.Value,
            Category = updateRequest.Category,
            IsActive = updateRequest.IsActive.Value
        };

        var warehouseStock = new WarehouseStock
        {
            ProductId = productId,
            Quantity = 25,
            InStock = true
        };

        _mockErpService.Setup(x => x.UpdateProductAsync(productId, It.IsAny<ErpProductRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ErpResponse<ErpProduct> { Success = true, Data = updatedErpProduct });

        _mockWarehouseService.Setup(x => x.GetStockAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WarehouseResponse<WarehouseStock> { Success = true, Data = warehouseStock });

        // Act
        var result = await _productService.UpdateProductAsync(productId, updateRequest);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(productId);
        result.Name.Should().Be("Updated Product");
        result.Price.Should().Be(39.99m);
        result.IsActive.Should().BeFalse();
        result.StockQuantity.Should().Be(25);
    }

    [Fact]
    public async Task UpdateProductAsync_WithNullProductId_ShouldThrowValidationException()
    {
        // Arrange
        var updateRequest = new UpdateProductRequest { Name = "Test" };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _productService.UpdateProductAsync(null!, updateRequest));
        
        exception.Message.Should().Be("Product ID cannot be null or empty");
    }

    [Fact]
    public async Task UpdateProductAsync_WhenProductNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        var productId = "nonexistent-product";
        var updateRequest = new UpdateProductRequest { Name = "Test" };

        _mockErpService.Setup(x => x.UpdateProductAsync(productId, It.IsAny<ErpProductRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ErpResponse<ErpProduct> { Success = false, ErrorMessage = "Product not found" });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _productService.UpdateProductAsync(productId, updateRequest));
        
        exception.Message.Should().Be($"Entity \"Product\" ({productId}) was not found.");
    }

    #endregion

    #region DeleteProductAsync Tests

    [Fact]
    public async Task DeleteProductAsync_WithValidId_ShouldReturnTrue()
    {
        // Arrange
        var productId = "test-product-123";

        _mockErpService.Setup(x => x.DeleteProductAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ErpResponse<bool> { Success = true, Data = true });

        // Act
        var result = await _productService.DeleteProductAsync(productId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteProductAsync_WithNullProductId_ShouldThrowValidationException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _productService.DeleteProductAsync(null!));
        
        exception.Message.Should().Be("Product ID cannot be null or empty");
    }

    [Fact]
    public async Task DeleteProductAsync_WhenProductNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        var productId = "nonexistent-product";

        _mockErpService.Setup(x => x.DeleteProductAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ErpResponse<bool> { Success = false, ErrorMessage = "Product not found" });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _productService.DeleteProductAsync(productId));
        
        exception.Message.Should().Be($"Entity \"Product\" ({productId}) was not found.");
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task GetProductAsync_WhenOperationCancelled_ShouldThrowOperationCancelledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _productService.GetProductAsync("test-id", cts.Token));
    }

    [Fact]
    public async Task GetProductsAsync_WhenErpServiceThrowsException_ShouldPropagateException()
    {
        // Arrange
        _mockErpService.Setup(x => x.GetProductsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _productService.GetProductsAsync());
        
        exception.Message.Should().Be("Database connection failed");
    }

    #endregion
}