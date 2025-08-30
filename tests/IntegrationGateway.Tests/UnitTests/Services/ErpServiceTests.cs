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
/// Unit tests for ErpService HTTP client exception handling
/// </summary>
public class ErpServiceTests : IDisposable
{
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<ILogger<ErpService>> _mockLogger;
    private readonly HttpClient _httpClient;
    private readonly ErpService _erpService;

    public ErpServiceTests()
    {
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockLogger = new Mock<ILogger<ErpService>>();

        _httpClient = new HttpClient(_mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:5051")
        };

        _mockHttpClientFactory.Setup(x => x.CreateClient("ErpClient"))
            .Returns(_httpClient);

        _erpService = new ErpService(_mockHttpClientFactory.Object, _mockLogger.Object);
    }

    #region GetProductAsync Tests

    [Fact]
    public async Task GetProductAsync_WithNullProductId_ShouldThrowValidationException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _erpService.GetProductAsync(null!));
        
        exception.Message.Should().Be("Product ID cannot be null or empty");
        exception.StatusCode.Should().Be(400);
        exception.ErrorType.Should().Be("validation_error");
    }

    [Fact]
    public async Task GetProductAsync_WithEmptyProductId_ShouldThrowValidationException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _erpService.GetProductAsync("   "));
        
        exception.Message.Should().Be("Product ID cannot be null or empty");
    }

    [Fact]
    public async Task GetProductAsync_WithValidId_ShouldReturnSuccess()
    {
        // Arrange
        var productId = "test-product-123";
        var expectedProduct = new ErpProduct
        {
            Id = productId,
            Name = "Test Product",
            Description = "Test Description",
            Price = 29.99m,
            Category = "Test Category",
            IsActive = true
        };

        var jsonResponse = JsonSerializer.Serialize(expectedProduct, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Get && 
                    req.RequestUri!.ToString().Contains($"/api/products/{productId}")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
            });

        // Act
        var result = await _erpService.GetProductAsync(productId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Id.Should().Be(productId);
        result.Data.Name.Should().Be("Test Product");
    }

    [Fact]
    public async Task GetProductAsync_WhenHttpRequestFails_ShouldReturnFailure()
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
        var result = await _erpService.GetProductAsync(productId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("HTTP request failed");
        result.Data.Should().BeNull();
    }

    [Fact]
    public async Task GetProductAsync_WhenNotFound_ShouldReturnFailure()
    {
        // Arrange
        var productId = "nonexistent-product";

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound,
                Content = new StringContent("Product not found", Encoding.UTF8, "text/plain")
            });

        // Act
        var result = await _erpService.GetProductAsync(productId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("HTTP request failed");
        result.Data.Should().BeNull();
    }

    [Fact]
    public async Task GetProductAsync_WhenHttpExceptionThrown_ShouldReturnFailure()
    {
        // Arrange
        var productId = "test-product-123";

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        var result = await _erpService.GetProductAsync(productId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Network error");
        result.Data.Should().BeNull();
    }

    [Fact]
    public async Task GetProductAsync_WhenTaskCancelled_ShouldReturnFailure()
    {
        // Arrange
        var productId = "test-product-123";

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Request was cancelled"));

        // Act
        var result = await _erpService.GetProductAsync(productId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Request was cancelled");
        result.Data.Should().BeNull();
    }

    #endregion

    #region GetProductsAsync Tests

    [Fact]
    public async Task GetProductsAsync_WhenSuccessful_ShouldReturnProductList()
    {
        // Arrange
        var expectedProducts = new List<ErpProduct>
        {
            new ErpProduct { Id = "1", Name = "Product 1", Price = 10.99m, Category = "Cat1", IsActive = true },
            new ErpProduct { Id = "2", Name = "Product 2", Price = 20.99m, Category = "Cat2", IsActive = false }
        };

        var jsonResponse = JsonSerializer.Serialize(expectedProducts, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Get && 
                    req.RequestUri!.ToString().Contains("/api/products")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
            });

        // Act
        var result = await _erpService.GetProductsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Should().HaveCount(2);
        result.Data[0].Id.Should().Be("1");
        result.Data[1].Id.Should().Be("2");
    }

    #endregion

    #region CreateProductAsync Tests

    [Fact]
    public async Task CreateProductAsync_WithNullRequest_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => _erpService.CreateProductAsync(null!));
        
        exception.ParamName.Should().Be("request");
    }

    [Fact]
    public async Task CreateProductAsync_WithValidRequest_ShouldReturnCreatedProduct()
    {
        // Arrange
        var createRequest = new ErpProductRequest
        {
            Name = "New Product",
            Description = "New Description",
            Price = 39.99m,
            Category = "New Category",
            IsActive = true
        };

        var createdProduct = new ErpProduct
        {
            Id = "new-product-123",
            Name = createRequest.Name,
            Description = createRequest.Description,
            Price = createRequest.Price,
            Category = createRequest.Category,
            IsActive = createRequest.IsActive
        };

        var jsonResponse = JsonSerializer.Serialize(createdProduct, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Post && 
                    req.RequestUri!.ToString().Contains("/api/products")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Created,
                Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
            });

        // Act
        var result = await _erpService.CreateProductAsync(createRequest);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Id.Should().Be("new-product-123");
        result.Data.Name.Should().Be("New Product");
    }

    #endregion

    #region UpdateProductAsync Tests

    [Fact]
    public async Task UpdateProductAsync_WithValidRequest_ShouldReturnUpdatedProduct()
    {
        // Arrange
        var productId = "test-product-123";
        var updateRequest = new ErpProductRequest
        {
            Name = "Updated Product",
            Description = "Updated Description",
            Price = 49.99m,
            Category = "Updated Category",
            IsActive = false
        };

        var updatedProduct = new ErpProduct
        {
            Id = productId,
            Name = updateRequest.Name,
            Description = updateRequest.Description,
            Price = updateRequest.Price,
            Category = updateRequest.Category,
            IsActive = updateRequest.IsActive
        };

        var jsonResponse = JsonSerializer.Serialize(updatedProduct, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Put && 
                    req.RequestUri!.ToString().Contains($"/api/products/{productId}")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
            });

        // Act
        var result = await _erpService.UpdateProductAsync(productId, updateRequest);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Name.Should().Be("Updated Product");
        result.Data.IsActive.Should().BeFalse();
    }

    #endregion

    #region DeleteProductAsync Tests

    [Fact]
    public async Task DeleteProductAsync_WithValidId_ShouldReturnSuccess()
    {
        // Arrange
        var productId = "test-product-123";

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Delete && 
                    req.RequestUri!.ToString().Contains($"/api/products/{productId}")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NoContent
            });

        // Act
        var result = await _erpService.DeleteProductAsync(productId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteProductAsync_WhenProductNotFound_ShouldReturnFailure()
    {
        // Arrange
        var productId = "nonexistent-product";

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound,
                Content = new StringContent("Product not found", Encoding.UTF8, "text/plain")
            });

        // Act
        var result = await _erpService.DeleteProductAsync(productId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("HTTP request failed");
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public async Task GetProductAsync_WithInvalidJson_ShouldReturnFailure()
    {
        // Arrange
        var productId = "test-product-123";
        var invalidJsonResponse = "{ invalid json }";

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
        var result = await _erpService.GetProductAsync(productId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Error deserializing response");
        result.Data.Should().BeNull();
    }

    [Fact]
    public async Task GetProductsAsync_WhenOperationCancelled_ShouldReturnFailure()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var result = await _erpService.GetProductsAsync(cts.Token);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Operation was canceled");
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