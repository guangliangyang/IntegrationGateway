using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using FluentAssertions;
using MediatR;
using IntegrationGateway.Api.Controllers.V1;
using IntegrationGateway.Application.Products.Queries;
using IntegrationGateway.Models.DTOs;
using IntegrationGateway.Models.Exceptions;

namespace IntegrationGateway.Tests.Controllers.V1;

/// <summary>
/// Unit tests for V1 ProductsController GetProducts endpoint
/// Tests MediatR integration, pagination, and error handling
/// </summary>
public class GetProductsControllerTests : IDisposable
{
    private readonly Mock<IMediator> _mockMediator;
    private readonly ProductsController _controller;
    private readonly WebApplicationFactory<Program> _factory;

    public GetProductsControllerTests()
    {
        _mockMediator = new Mock<IMediator>();
        _controller = new ProductsController(_mockMediator.Object);
        
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Test");
                builder.ConfigureServices(services =>
                {
                    // Replace services with mocks for testing
                    services.AddScoped(_ => _mockMediator.Object);
                });
            });
    }

    [Fact]
    public async Task GetProducts_WithDefaultParameters_ShouldReturnOkWithProductList()
    {
        // Arrange
        var expectedResponse = new ProductListResponse
        {
            Products = new List<ProductDto>
            {
                new ProductDto
                {
                    Id = "1",
                    Name = "Test Product 1",
                    Description = "Test Description 1",
                    Price = 10.99m,
                    Category = "Test Category",
                    IsActive = true,
                    StockQuantity = 50,
                    InStock = true
                },
                new ProductDto
                {
                    Id = "2",
                    Name = "Test Product 2",
                    Description = "Test Description 2",
                    Price = 25.50m,
                    Category = "Test Category",
                    IsActive = true,
                    StockQuantity = 30,
                    InStock = true
                }
            },
            Total = 2,
            Page = 1,
            PageSize = 50
        };

        _mockMediator
            .Setup(m => m.Send(It.Is<GetProductsV1Query>(q => q.Page == 1 && q.PageSize == 50), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.GetProducts();

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<OkObjectResult>();

        var okResult = result.Result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(expectedResponse);

        _mockMediator.Verify(m => m.Send(It.Is<GetProductsV1Query>(q => q.Page == 1 && q.PageSize == 50), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetProducts_WithCustomPagination_ShouldPassCorrectParameters()
    {
        // Arrange
        const int customPage = 3;
        const int customPageSize = 20;

        var expectedResponse = new ProductListResponse
        {
            Products = new List<ProductDto>(),
            Total = 0,
            Page = customPage,
            PageSize = customPageSize
        };

        _mockMediator
            .Setup(m => m.Send(It.Is<GetProductsV1Query>(q => q.Page == customPage && q.PageSize == customPageSize), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.GetProducts(customPage, customPageSize);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<OkObjectResult>();

        var okResult = result.Result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(expectedResponse);

        _mockMediator.Verify(m => m.Send(It.Is<GetProductsV1Query>(q => q.Page == customPage && q.PageSize == customPageSize), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetProducts_WithCancellationToken_ShouldPassTokenToMediator()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        var expectedResponse = new ProductListResponse
        {
            Products = new List<ProductDto>(),
            Total = 0,
            Page = 1,
            PageSize = 50
        };

        _mockMediator
            .Setup(m => m.Send(It.IsAny<GetProductsV1Query>(), cancellationToken))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.GetProducts(cancellationToken: cancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<OkObjectResult>();

        _mockMediator.Verify(m => m.Send(It.IsAny<GetProductsV1Query>(), cancellationToken), Times.Once);
    }

    [Fact]
    public async Task GetProducts_WithLargeProductList_ShouldReturnAllProducts()
    {
        // Arrange
        var products = Enumerable.Range(1, 100)
            .Select(i => new ProductDto
            {
                Id = i.ToString(),
                Name = $"Product {i}",
                Description = $"Description for product {i}",
                Price = i * 10.99m,
                Category = $"Category {i % 5}",
                IsActive = i % 2 == 0,
                StockQuantity = i * 5,
                InStock = i % 3 != 0
            })
            .ToList();

        var expectedResponse = new ProductListResponse
        {
            Products = products,
            Total = 100,
            Page = 1,
            PageSize = 100
        };

        _mockMediator
            .Setup(m => m.Send(It.Is<GetProductsV1Query>(q => q.Page == 1 && q.PageSize == 100), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.GetProducts(1, 100);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<OkObjectResult>();

        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as ProductListResponse;
        
        response.Should().NotBeNull();
        response!.Products.Should().HaveCount(100);
        response.Total.Should().Be(100);
        response.Products.Should().BeEquivalentTo(products);
    }

    [Fact]
    public async Task GetProducts_WithZeroResults_ShouldReturnEmptyList()
    {
        // Arrange
        var expectedResponse = new ProductListResponse
        {
            Products = new List<ProductDto>(),
            Total = 0,
            Page = 1,
            PageSize = 50
        };

        _mockMediator
            .Setup(m => m.Send(It.IsAny<GetProductsV1Query>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.GetProducts();

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<OkObjectResult>();

        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as ProductListResponse;
        
        response.Should().NotBeNull();
        response!.Products.Should().BeEmpty();
        response.Total.Should().Be(0);
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(-1, 50)]
    [InlineData(1, 0)]
    [InlineData(1, -10)]
    public async Task GetProducts_WithInvalidPagination_ShouldStillCallMediator(int page, int pageSize)
    {
        // Arrange
        var expectedResponse = new ProductListResponse
        {
            Products = new List<ProductDto>(),
            Total = 0,
            Page = page,
            PageSize = pageSize
        };

        _mockMediator
            .Setup(m => m.Send(It.Is<GetProductsV1Query>(q => q.Page == page && q.PageSize == pageSize), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.GetProducts(page, pageSize);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<OkObjectResult>();

        _mockMediator.Verify(m => m.Send(It.Is<GetProductsV1Query>(q => q.Page == page && q.PageSize == pageSize), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetProducts_WhenMediatorThrowsException_ShouldPropagateException()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Database connection failed");

        _mockMediator
            .Setup(m => m.Send(It.IsAny<GetProductsV1Query>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _controller.GetProducts());

        exception.Should().BeSameAs(expectedException);
        exception.Message.Should().Be("Database connection failed");

        _mockMediator.Verify(m => m.Send(It.IsAny<GetProductsV1Query>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetProducts_WhenOperationCancelled_ShouldThrowOperationCancelledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockMediator
            .Setup(m => m.Send(It.IsAny<GetProductsV1Query>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _controller.GetProducts(cancellationToken: cts.Token));

        _mockMediator.Verify(m => m.Send(It.IsAny<GetProductsV1Query>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void GetProductsQuery_ShouldHaveCacheableAttribute()
    {
        // Arrange & Act
        var queryType = typeof(GetProductsV1Query);
        var cacheableAttribute = queryType.GetCustomAttributes(typeof(IntegrationGateway.Application.Common.Behaviours.CacheableAttribute), false)
            .FirstOrDefault() as IntegrationGateway.Application.Common.Behaviours.CacheableAttribute;

        // Assert
        cacheableAttribute.Should().NotBeNull("GetProductsQuery should have Cacheable attribute");
        cacheableAttribute!.DurationSeconds.Should().Be(5, "Cache should be set to 5 seconds");
    }

    #region Exception Handling Integration Tests

    [Fact]
    public async Task GetProducts_WhenValidationExceptionThrown_ShouldReturn400WithProblemDetails()
    {
        // Arrange
        var client = _factory.CreateClient();
        
        _mockMediator
            .Setup(m => m.Send(It.IsAny<GetProductsV1Query>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("Invalid pagination parameters"));

        // Act
        var response = await client.GetAsync("/api/v1/products?page=-1&pageSize=0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        var problemDetails = JsonSerializer.Deserialize<JsonElement>(responseContent);
        
        problemDetails.TryGetProperty("status", out var status).Should().BeTrue();
        status.GetInt32().Should().Be(400);
        
        problemDetails.TryGetProperty("errorType", out var errorType).Should().BeTrue();
        errorType.GetString().Should().Be("validation_error");
    }

    [Fact]
    public async Task GetProducts_WhenExternalServiceExceptionThrown_ShouldReturn502WithProblemDetails()
    {
        // Arrange
        var client = _factory.CreateClient();
        
        _mockMediator
            .Setup(m => m.Send(It.IsAny<GetProductsV1Query>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ExternalServiceException("ERP", "ERP service is unavailable"));

        // Act
        var response = await client.GetAsync("/api/v1/products");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        var problemDetails = JsonSerializer.Deserialize<JsonElement>(responseContent);
        
        problemDetails.TryGetProperty("status", out var status).Should().BeTrue();
        status.GetInt32().Should().Be(502);
        
        problemDetails.TryGetProperty("errorType", out var errorType).Should().BeTrue();
        errorType.GetString().Should().Be("external_service_error");
    }

    [Fact]
    public async Task GetProducts_WhenServiceUnavailableExceptionThrown_ShouldReturn503WithProblemDetails()
    {
        // Arrange
        var client = _factory.CreateClient();
        
        _mockMediator
            .Setup(m => m.Send(It.IsAny<GetProductsV1Query>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ServiceUnavailableException("Service is temporarily down for maintenance"));

        // Act
        var response = await client.GetAsync("/api/v1/products");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        var problemDetails = JsonSerializer.Deserialize<JsonElement>(responseContent);
        
        problemDetails.TryGetProperty("status", out var status).Should().BeTrue();
        status.GetInt32().Should().Be(503);
        
        problemDetails.TryGetProperty("errorType", out var errorType).Should().BeTrue();
        errorType.GetString().Should().Be("service_unavailable");
    }

    [Fact]
    public async Task GetProducts_WhenTaskCancelledExceptionThrown_ShouldReturn408WithProblemDetails()
    {
        // Arrange
        var client = _factory.CreateClient();
        
        _mockMediator
            .Setup(m => m.Send(It.IsAny<GetProductsV1Query>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("Request timed out"));

        // Act
        var response = await client.GetAsync("/api/v1/products");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.RequestTimeout);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        var problemDetails = JsonSerializer.Deserialize<JsonElement>(responseContent);
        
        problemDetails.TryGetProperty("status", out var status).Should().BeTrue();
        status.GetInt32().Should().Be(408);
        
        problemDetails.TryGetProperty("errorType", out var errorType).Should().BeTrue();
        errorType.GetString().Should().Be("request_timeout");
    }

    #endregion

    public void Dispose()
    {
        _factory?.Dispose();
    }
}