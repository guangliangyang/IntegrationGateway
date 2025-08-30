using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using FluentAssertions;
using IntegrationGateway.Services.Implementation;
using IntegrationGateway.Models.External;
using IntegrationGateway.Models.Exceptions;

namespace IntegrationGateway.Tests.UnitTests.Services;

/// <summary>
/// Unit tests for WarehouseService external service exception handling and fallback logic
/// </summary>
public class WarehouseServiceTests : IDisposable
{
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<ILogger<WarehouseService>> _mockLogger;
    private readonly HttpClient _httpClient;
    private readonly WarehouseService _warehouseService;

    public WarehouseServiceTests()
    {
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockLogger = new Mock<ILogger<WarehouseService>>();

        _httpClient = new HttpClient(_mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:5052")
        };

        _mockHttpClientFactory.Setup(x => x.CreateClient("WarehouseClient"))
            .Returns(_httpClient);

        _warehouseService = new WarehouseService(_mockHttpClientFactory.Object, _mockLogger.Object);
    }

    #region GetStockAsync Tests

    [Fact]
    public async Task GetStockAsync_WithNullProductId_ShouldThrowValidationException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _warehouseService.GetStockAsync(null!));
        
        exception.Message.Should().Be("Product ID cannot be null or empty");
        exception.StatusCode.Should().Be(400);
        exception.ErrorType.Should().Be("validation_error");
    }

    [Fact]
    public async Task GetStockAsync_WithEmptyProductId_ShouldThrowValidationException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _warehouseService.GetStockAsync("   "));
        
        exception.Message.Should().Be("Product ID cannot be null or empty");
    }

    [Fact]
    public async Task GetStockAsync_WithValidId_ShouldReturnStock()
    {
        // Arrange
        var productId = "test-product-123";
        var expectedStock = new WarehouseStock
        {
            ProductId = productId,
            Quantity = 50,
            InStock = true,
            LastUpdated = DateTime.UtcNow
        };

        var jsonResponse = JsonSerializer.Serialize(expectedStock, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Get && 
                    req.RequestUri!.ToString().Contains($"/api/stock/{productId}")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
            });

        // Act
        var result = await _warehouseService.GetStockAsync(productId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.ProductId.Should().Be(productId);
        result.Data.Quantity.Should().Be(50);
        result.Data.InStock.Should().BeTrue();
    }

    [Fact]
    public async Task GetStockAsync_WhenHttpRequestFails_ShouldReturnFallbackStock()
    {
        // Arrange
        var productId = "test-product-123";

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Content = new StringContent("Internal server error", Encoding.UTF8, "text/plain")
            });

        // Act
        var result = await _warehouseService.GetStockAsync(productId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue(); // Fallback should return success with default stock
        result.Data.Should().NotBeNull();
        result.Data!.ProductId.Should().Be(productId);
        result.Data.Quantity.Should().Be(0); // Default fallback quantity
        result.Data.InStock.Should().BeFalse(); // Default fallback stock status
    }

    [Fact]
    public async Task GetStockAsync_WhenNetworkException_ShouldReturnFallbackStock()
    {
        // Arrange
        var productId = "test-product-123";

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network connection failed"));

        // Act
        var result = await _warehouseService.GetStockAsync(productId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue(); // Fallback should return success
        result.Data.Should().NotBeNull();
        result.Data!.ProductId.Should().Be(productId);
        result.Data.Quantity.Should().Be(0);
        result.Data.InStock.Should().BeFalse();
    }

    [Fact]
    public async Task GetStockAsync_WhenTimeout_ShouldReturnFallbackStock()
    {
        // Arrange
        var productId = "test-product-123";

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Request timed out", new TimeoutException()));

        // Act
        var result = await _warehouseService.GetStockAsync(productId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue(); // Fallback should return success
        result.Data.Should().NotBeNull();
        result.Data!.ProductId.Should().Be(productId);
        result.Data.Quantity.Should().Be(0);
    }

    #endregion

    #region GetBulkStockAsync Tests

    [Fact]
    public async Task GetBulkStockAsync_WithNullProductIds_ShouldThrowValidationException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _warehouseService.GetBulkStockAsync(null!));
        
        exception.Message.Should().Be("Product IDs list cannot be null");
        exception.StatusCode.Should().Be(400);
        exception.ErrorType.Should().Be("validation_error");
    }

    [Fact]
    public async Task GetBulkStockAsync_WithEmptyList_ShouldReturnEmptyResponse()
    {
        // Arrange
        var emptyList = new List<string>();

        // Act
        var result = await _warehouseService.GetBulkStockAsync(emptyList);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Stocks.Should().BeEmpty();
    }

    [Fact]
    public async Task GetBulkStockAsync_WithTooManyProducts_ShouldThrowValidationException()
    {
        // Arrange
        var tooManyProducts = Enumerable.Range(1, 1001).Select(i => i.ToString()).ToList();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _warehouseService.GetBulkStockAsync(tooManyProducts));
        
        exception.Message.Should().Be("Cannot request more than 1000 products at once");
    }

    [Fact]
    public async Task GetBulkStockAsync_WithValidIds_ShouldReturnBulkStock()
    {
        // Arrange
        var productIds = new List<string> { "product1", "product2", "product3" };
        var expectedStocks = new List<WarehouseStock>
        {
            new WarehouseStock { ProductId = "product1", Quantity = 10, InStock = true },
            new WarehouseStock { ProductId = "product2", Quantity = 5, InStock = true },
            new WarehouseStock { ProductId = "product3", Quantity = 0, InStock = false }
        };

        var bulkResponse = new BulkStockResponse { Stocks = expectedStocks };
        var jsonResponse = JsonSerializer.Serialize(bulkResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Post && 
                    req.RequestUri!.ToString().Contains("/api/stock/bulk")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
            });

        // Act
        var result = await _warehouseService.GetBulkStockAsync(productIds);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Stocks.Should().HaveCount(3);
        result.Data.Stocks.Should().Contain(s => s.ProductId == "product1" && s.Quantity == 10);
        result.Data.Stocks.Should().Contain(s => s.ProductId == "product2" && s.Quantity == 5);
        result.Data.Stocks.Should().Contain(s => s.ProductId == "product3" && s.Quantity == 0);
    }

    [Fact]
    public async Task GetBulkStockAsync_WhenServiceUnavailable_ShouldReturnFallbackStock()
    {
        // Arrange
        var productIds = new List<string> { "product1", "product2" };

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.ServiceUnavailable,
                Content = new StringContent("Service temporarily unavailable", Encoding.UTF8, "text/plain")
            });

        // Act
        var result = await _warehouseService.GetBulkStockAsync(productIds);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue(); // Fallback should return success
        result.Data.Should().NotBeNull();
        result.Data!.Stocks.Should().HaveCount(2);
        
        // All stocks should have fallback values
        result.Data.Stocks.Should().AllSatisfy(stock =>
        {
            stock.Quantity.Should().Be(0);
            stock.InStock.Should().BeFalse();
            productIds.Should().Contain(stock.ProductId);
        });
    }

    [Fact]
    public async Task GetBulkStockAsync_WhenJsonDeserializationFails_ShouldReturnFallbackStock()
    {
        // Arrange
        var productIds = new List<string> { "product1" };
        var invalidJsonResponse = "{ invalid json response }";

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(invalidJsonResponse, Encoding.UTF8, "application/json")
            });

        // Act
        var result = await _warehouseService.GetBulkStockAsync(productIds);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue(); // Fallback should return success
        result.Data.Should().NotBeNull();
        result.Data!.Stocks.Should().HaveCount(1);
        result.Data.Stocks[0].ProductId.Should().Be("product1");
        result.Data.Stocks[0].Quantity.Should().Be(0);
        result.Data.Stocks[0].InStock.Should().BeFalse();
    }

    #endregion

    #region Fallback Logic Tests

    [Fact]
    public async Task GetStockAsync_ShouldLogWarningWhenUsingFallback()
    {
        // Arrange
        var productId = "test-product-123";

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Service unavailable"));

        // Act
        var result = await _warehouseService.GetStockAsync(productId);

        // Assert
        result.Success.Should().BeTrue();
        
        // Verify that warning was logged about using fallback
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("fallback") || v.ToString()!.Contains("default")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task GetBulkStockAsync_WithVariousListSizes_ShouldReturnCorrectFallbackCount(int productCount)
    {
        // Arrange
        var productIds = Enumerable.Range(1, productCount).Select(i => $"product{i}").ToList();

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        var result = await _warehouseService.GetBulkStockAsync(productIds);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Stocks.Should().HaveCount(productCount);
        
        // Verify all products have fallback stock
        foreach (var expectedProductId in productIds)
        {
            result.Data.Stocks.Should().Contain(s => s.ProductId == expectedProductId);
        }
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public async Task GetStockAsync_WhenOperationCancelled_ShouldReturnFallback()
    {
        // Arrange
        var productId = "test-product-123";
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var result = await _warehouseService.GetStockAsync(productId, cts.Token);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue(); // Fallback should still work
        result.Data.Should().NotBeNull();
        result.Data!.ProductId.Should().Be(productId);
    }

    [Fact]
    public async Task GetBulkStockAsync_WhenPartialResponseReceived_ShouldHandleGracefully()
    {
        // Arrange
        var productIds = new List<string> { "product1", "product2", "product3" };
        
        // Response only contains 2 out of 3 requested products
        var partialStocks = new List<WarehouseStock>
        {
            new WarehouseStock { ProductId = "product1", Quantity = 10, InStock = true },
            new WarehouseStock { ProductId = "product2", Quantity = 5, InStock = true }
            // Missing product3
        };

        var bulkResponse = new BulkStockResponse { Stocks = partialStocks };
        var jsonResponse = JsonSerializer.Serialize(bulkResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
            });

        // Act
        var result = await _warehouseService.GetBulkStockAsync(productIds);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Stocks.Should().HaveCount(2); // Only the products that were returned
    }

    #endregion

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _httpClient?.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}