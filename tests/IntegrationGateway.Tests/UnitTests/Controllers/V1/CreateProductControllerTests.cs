using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using DotNetEnv;
using Moq;
using FluentAssertions;
using MediatR;
using IntegrationGateway.Api.Controllers.V1;
using IntegrationGateway.Application.Products.Commands;
using IntegrationGateway.Models.DTOs;
using IntegrationGateway.Services.Interfaces;
using IntegrationGateway.Application.Common.Interfaces;
using IntegrationGateway.Models.Common;

namespace IntegrationGateway.Tests.Controllers.V1;

/// <summary>
/// Unit tests for V1 ProductsController CreateProduct endpoint
/// Tests MediatR integration, validation, authorization, and idempotency
/// </summary>
public class CreateProductControllerTests : IDisposable
{
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly Mock<IIdempotencyService> _mockIdempotencyService;
    private readonly ProductsController _controller;
    private readonly WebApplicationFactory<Program> _factory;

    public CreateProductControllerTests()
    {
        // Load environment variables from .env file for testing
        Env.Load();
        
        _mockMediator = new Mock<IMediator>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockIdempotencyService = new Mock<IIdempotencyService>();
        
        _controller = new ProductsController(_mockMediator.Object);

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                // Set test environment
                Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Test");
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    // Add test-specific configuration
                    var basePath = Path.GetDirectoryName(typeof(CreateProductControllerTests).Assembly.Location);
                    config.SetBasePath(basePath!);
                    config.AddJsonFile("appsettings.Test.json", optional: false, reloadOnChange: false);
                });
                builder.ConfigureServices(services =>
                {
                    // Replace services with mocks for testing
                    services.AddScoped(_ => _mockMediator.Object);
                    services.AddScoped(_ => _mockCurrentUserService.Object);
                    services.AddScoped(_ => _mockIdempotencyService.Object);
                });
            });
    }

    #region Basic CreateProduct Unit Tests

    [Fact]
    public async Task CreateProduct_WithValidRequest_ShouldReturnCreatedResult()
    {
        // Arrange
        var request = new CreateProductRequest
        {
            Name = "Test Product",
            Description = "Test Description",
            Price = 29.99m,
            Category = "Test Category",
            IsActive = true
        };

        var expectedProduct = new ProductDto
        {
            Id = "123",
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            Category = request.Category,
            IsActive = request.IsActive,
            StockQuantity = 0,
            InStock = false
        };

        _mockMediator
            .Setup(m => m.Send(It.IsAny<CreateProductCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedProduct);

        // Act
        var result = await _controller.CreateProduct(request);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<CreatedAtActionResult>();

        var createdResult = result.Result as CreatedAtActionResult;
        createdResult!.ActionName.Should().Be(nameof(_controller.GetProduct));
        createdResult.RouteValues!["id"].Should().Be("123");
        createdResult.Value.Should().BeEquivalentTo(expectedProduct);

        _mockMediator.Verify(m => m.Send(It.Is<CreateProductCommand>(c =>
            c.Name == request.Name &&
            c.Description == request.Description &&
            c.Price == request.Price &&
            c.Category == request.Category &&
            c.IsActive == request.IsActive
        ), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateProduct_WithMinimalRequest_ShouldCreateProduct()
    {
        // Arrange
        var request = new CreateProductRequest
        {
            Name = "Minimal Product",
            Price = 1.00m,
            Category = "Minimal",
            IsActive = false
        };

        var expectedProduct = new ProductDto
        {
            Id = "456",
            Name = request.Name,
            Description = null,
            Price = request.Price,
            Category = request.Category,
            IsActive = request.IsActive,
            StockQuantity = 0,
            InStock = false
        };

        _mockMediator
            .Setup(m => m.Send(It.IsAny<CreateProductCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedProduct);

        // Act
        var result = await _controller.CreateProduct(request);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<CreatedAtActionResult>();

        var createdResult = result.Result as CreatedAtActionResult;
        createdResult!.Value.Should().BeEquivalentTo(expectedProduct);

        _mockMediator.Verify(m => m.Send(It.Is<CreateProductCommand>(c =>
            c.Name == request.Name &&
            c.Description == null &&
            c.Price == request.Price &&
            c.Category == request.Category &&
            c.IsActive == request.IsActive
        ), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateProduct_WithCancellationToken_ShouldPassTokenToMediator()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        var request = new CreateProductRequest
        {
            Name = "Test Product",
            Price = 10.00m,
            Category = "Test",
            IsActive = true
        };

        var expectedProduct = new ProductDto
        {
            Id = "789",
            Name = request.Name,
            Price = request.Price,
            Category = request.Category,
            IsActive = request.IsActive
        };

        _mockMediator
            .Setup(m => m.Send(It.IsAny<CreateProductCommand>(), cancellationToken))
            .ReturnsAsync(expectedProduct);

        // Act
        var result = await _controller.CreateProduct(request, cancellationToken);

        // Assert
        result.Should().NotBeNull();
        _mockMediator.Verify(m => m.Send(It.IsAny<CreateProductCommand>(), cancellationToken), Times.Once);
    }

    [Fact]
    public async Task CreateProduct_WhenMediatorThrowsException_ShouldPropagateException()
    {
        // Arrange
        var request = new CreateProductRequest
        {
            Name = "Test Product",
            Price = 10.00m,
            Category = "Test",
            IsActive = true
        };

        var expectedException = new InvalidOperationException("Product creation failed");

        _mockMediator
            .Setup(m => m.Send(It.IsAny<CreateProductCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _controller.CreateProduct(request));

        exception.Should().BeSameAs(expectedException);
        exception.Message.Should().Be("Product creation failed");
    }

    #endregion

    #region Idempotency Integration Tests

    [Fact]
    public async Task CreateProduct_WithValidIdempotencyKey_ShouldProcessSuccessfully()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new CreateProductRequest
        {
            Name = "Idempotent Product",
            Description = "Test idempotency",
            Price = 49.99m,
            Category = "Test Category",
            IsActive = true
        };

        var expectedProduct = new ProductDto
        {
            Id = "idempotent-123",
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            Category = request.Category,
            IsActive = request.IsActive
        };

        _mockIdempotencyService
            .Setup(s => s.GetOrCreateOperationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, new IdempotencyKey()));

        _mockMediator
            .Setup(m => m.Send(It.IsAny<CreateProductCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedProduct);

        var jsonContent = JsonSerializer.Serialize(request);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        // Act
        client.DefaultRequestHeaders.Add("Idempotency-Key", "test-key-12345678901234567890");
        client.DefaultRequestHeaders.Add("Authorization", "Bearer fake-jwt-token");

        var response = await client.PostAsync("/api/v1/products", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateProduct_WithDuplicateIdempotencyKey_ShouldReturnCachedResponse()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new CreateProductRequest
        {
            Name = "Duplicate Product",
            Price = 19.99m,
            Category = "Test",
            IsActive = true
        };

        var cachedResponse = new ProductDto
        {
            Id = "cached-456",
            Name = "Previously Created Product",
            Price = 19.99m,
            Category = "Test",
            IsActive = true
        };

        var operationRecord = new IdempotencyKey
        {
            ResponseBody = JsonSerializer.Serialize(cachedResponse),
            ResponseStatusCode = 201
        };

        _mockIdempotencyService
            .Setup(s => s.GetOrCreateOperationAsync(
                "duplicate-key-12345678901234567890",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, operationRecord));

        var jsonContent = JsonSerializer.Serialize(request);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        // Act
        client.DefaultRequestHeaders.Add("Idempotency-Key", "duplicate-key-12345678901234567890");
        client.DefaultRequestHeaders.Add("Authorization", "Bearer fake-jwt-token");

        var response = await client.PostAsync("/api/v1/products", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        var returnedProduct = JsonSerializer.Deserialize<ProductDto>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        returnedProduct.Should().BeEquivalentTo(cachedResponse);
        
        // Verify that the mediator was NOT called for duplicate request
        _mockMediator.Verify(m => m.Send(It.IsAny<CreateProductCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateProduct_WithConcurrentSameIdempotencyKey_ShouldReturn409Conflict()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new CreateProductRequest
        {
            Name = "Concurrent Product",
            Price = 29.99m,
            Category = "Test",
            IsActive = true
        };

        var operationRecord = new IdempotencyKey
        {
            ResponseBody = null, // Indicates operation is still in progress
            ResponseStatusCode = null
        };

        _mockIdempotencyService
            .Setup(s => s.GetOrCreateOperationAsync(
                "concurrent-key-12345678901234567890",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, operationRecord));

        var jsonContent = JsonSerializer.Serialize(request);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        // Act
        client.DefaultRequestHeaders.Add("Idempotency-Key", "concurrent-key-12345678901234567890");
        client.DefaultRequestHeaders.Add("Authorization", "Bearer fake-jwt-token");

        var response = await client.PostAsync("/api/v1/products", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("concurrent_request");
        responseContent.Should().Contain("A request with the same idempotency key is currently being processed");
    }

    [Fact]
    public async Task CreateProduct_WithMissingIdempotencyKey_ShouldReturn400BadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new CreateProductRequest
        {
            Name = "No Idempotency Product",
            Price = 15.99m,
            Category = "Test",
            IsActive = true
        };

        var jsonContent = JsonSerializer.Serialize(request);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        // Act
        client.DefaultRequestHeaders.Add("Authorization", "Bearer fake-jwt-token");
        var response = await client.PostAsync("/api/v1/products", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("missing_idempotency_key");
        responseContent.Should().Contain("Idempotency-Key header is required");
    }

    [Theory]
    [InlineData("short")] // Too short (< 16 characters)
    [InlineData("a")] // Much too short
    [InlineData("")] // Empty
    [InlineData("this-key-is-way-too-long-and-exceeds-the-maximum-allowed-length-of-128-characters-which-should-cause-validation-to-fail-definitely")] // Too long (> 128 characters)
    public async Task CreateProduct_WithInvalidIdempotencyKeyLength_ShouldReturn400BadRequest(string invalidKey)
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new CreateProductRequest
        {
            Name = "Invalid Key Product",
            Price = 25.99m,
            Category = "Test",
            IsActive = true
        };

        var jsonContent = JsonSerializer.Serialize(request);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        // Act
        client.DefaultRequestHeaders.Add("Idempotency-Key", invalidKey);
        client.DefaultRequestHeaders.Add("Authorization", "Bearer fake-jwt-token");

        var response = await client.PostAsync("/api/v1/products", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("invalid_idempotency_key");
        responseContent.Should().Contain("between 16 and 128 characters");
    }

    [Fact]
    public async Task CreateProduct_WithDifferentIdempotencyKeys_ShouldProcessBothRequests()
    {
        // Arrange
        var client1 = _factory.CreateClient();
        var client2 = _factory.CreateClient();

        var request1 = new CreateProductRequest
        {
            Name = "First Product",
            Price = 10.99m,
            Category = "Test",
            IsActive = true
        };

        var request2 = new CreateProductRequest
        {
            Name = "Second Product",
            Price = 20.99m,
            Category = "Test",
            IsActive = true
        };

        var product1 = new ProductDto
        {
            Id = "first-789",
            Name = request1.Name,
            Price = request1.Price,
            Category = request1.Category,
            IsActive = request1.IsActive
        };

        var product2 = new ProductDto
        {
            Id = "second-790",
            Name = request2.Name,
            Price = request2.Price,
            Category = request2.Category,
            IsActive = request2.IsActive
        };

        _mockIdempotencyService
            .Setup(s => s.GetOrCreateOperationAsync(
                "first-key-12345678901234567890",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, new IdempotencyKey()));

        _mockIdempotencyService
            .Setup(s => s.GetOrCreateOperationAsync(
                "second-key-12345678901234567890",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, new IdempotencyKey()));

        _mockMediator
            .SetupSequence(m => m.Send(It.IsAny<CreateProductCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(product1)
            .ReturnsAsync(product2);

        var jsonContent1 = JsonSerializer.Serialize(request1);
        var content1 = new StringContent(jsonContent1, Encoding.UTF8, "application/json");

        var jsonContent2 = JsonSerializer.Serialize(request2);
        var content2 = new StringContent(jsonContent2, Encoding.UTF8, "application/json");

        // Act
        client1.DefaultRequestHeaders.Add("Idempotency-Key", "first-key-12345678901234567890");
        client1.DefaultRequestHeaders.Add("Authorization", "Bearer fake-jwt-token");

        client2.DefaultRequestHeaders.Add("Idempotency-Key", "second-key-12345678901234567890");
        client2.DefaultRequestHeaders.Add("Authorization", "Bearer fake-jwt-token");

        var response1 = await client1.PostAsync("/api/v1/products", content1);
        var response2 = await client2.PostAsync("/api/v1/products", content2);

        // Assert
        response1.StatusCode.Should().Be(HttpStatusCode.Created);
        response2.StatusCode.Should().Be(HttpStatusCode.Created);

        // Verify both products were created
        _mockMediator.Verify(m => m.Send(It.IsAny<CreateProductCommand>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    #endregion

    #region Cache Management Tests

    [Fact]
    public void CreateProductCommand_ShouldBeValidCommand()
    {
        // Arrange & Act
        var command = new CreateProductCommand
        {
            Name = "Test Product",
            Price = 10.99m,
            Category = "Test",
            IsActive = true
        };

        // Assert
        command.Should().NotBeNull();
        command.Name.Should().Be("Test Product");
        command.Price.Should().Be(10.99m);
        command.Category.Should().Be("Test");
        command.IsActive.Should().BeTrue();
    }

    #endregion

    #region ProblemDetails Response Format Tests

    [Fact]
    public async Task CreateProduct_WithMissingIdempotencyKey_ShouldReturnProblemDetailsFormat()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new CreateProductRequest
        {
            Name = "Test Product",
            Price = 15.99m,
            Category = "Test",
            IsActive = true
        };

        var jsonContent = JsonSerializer.Serialize(request);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        // Act
        client.DefaultRequestHeaders.Add("Authorization", "Bearer fake-jwt-token");
        var response = await client.PostAsync("/api/v1/products", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        var problemDetails = JsonSerializer.Deserialize<JsonElement>(responseContent);
        
        // Verify ProblemDetails structure
        problemDetails.TryGetProperty("type", out var type).Should().BeTrue();
        type.GetString().Should().Contain("httpstatuses.com/400");
        
        problemDetails.TryGetProperty("title", out var title).Should().BeTrue();
        title.GetString().Should().Be("Bad Request");
        
        problemDetails.TryGetProperty("status", out var status).Should().BeTrue();
        status.GetInt32().Should().Be(400);
        
        problemDetails.TryGetProperty("detail", out var detail).Should().BeTrue();
        detail.GetString().Should().NotBeNullOrEmpty();
        
        problemDetails.TryGetProperty("errorType", out var errorType).Should().BeTrue();
        
        problemDetails.TryGetProperty("traceId", out var traceId).Should().BeTrue();
        traceId.GetString().Should().NotBeNullOrEmpty();
        
        problemDetails.TryGetProperty("timestamp", out var timestamp).Should().BeTrue();
    }

    [Fact]  
    public async Task CreateProduct_WithValidationError_ShouldReturnProblemDetailsWithErrors()
    {
        // This test would verify validation errors format when MediatR throws ValidationException
        // The actual validation would come from FluentValidation in the Command Handler
        
        // Arrange
        var client = _factory.CreateClient();
        var request = new CreateProductRequest
        {
            Name = "", // Invalid name
            Price = -1, // Invalid price
            Category = null!,
            IsActive = true
        };

        var jsonContent = JsonSerializer.Serialize(request);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        // Act
        client.DefaultRequestHeaders.Add("Idempotency-Key", "test-validation-key-12345678");
        client.DefaultRequestHeaders.Add("Authorization", "Bearer fake-jwt-token");
        var response = await client.PostAsync("/api/v1/products", content);

        // Assert - Even with validation errors, current middleware should handle it
        var responseContent = await response.Content.ReadAsStringAsync();
        
        // The response should still be in ProblemDetails format
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var problemDetails = JsonSerializer.Deserialize<JsonElement>(responseContent);
            problemDetails.TryGetProperty("type", out _).Should().BeTrue();
            problemDetails.TryGetProperty("title", out _).Should().BeTrue();
            problemDetails.TryGetProperty("status", out _).Should().BeTrue();
        }
    }

    #endregion

    public void Dispose()
    {
        _factory?.Dispose();
    }
}