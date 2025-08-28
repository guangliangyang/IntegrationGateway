using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using FluentAssertions;
using IntegrationGateway.Models.DTOs;
using IntegrationGateway.Services.Interfaces;
using Moq;
using WireMock.Server;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using Microsoft.AspNetCore.Hosting;

namespace IntegrationGateway.Tests.Integration;

/// <summary>
/// Integration tests for V1 Products API endpoints
/// Tests full HTTP pipeline including Application Insights telemetry
/// </summary>
public class V1ProductsIntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly WireMockServer _erpMockServer;
    private readonly WireMockServer _warehouseMockServer;

    public V1ProductsIntegrationTests(WebApplicationFactory<Program> factory)
    {
        // Start WireMock servers for external dependencies
        _erpMockServer = WireMockServer.Start(5051);
        _warehouseMockServer = WireMockServer.Start(5052);

        _factory = factory.WithWebHostBuilder(builder =>
        {
            // Configure Application Insights from .env file
            builder.ConfigureApplicationInsights();
            
            builder.ConfigureLogging(logging =>
            {
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Information);
            });
        });

        _client = _factory.CreateClient();
        SetupMockServices();
    }

    private void SetupMockServices()
    {
        // Setup ERP service mock responses
        _erpMockServer
            .Given(Request.Create().WithPath("/api/products").UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(JsonSerializer.Serialize(new
                    {
                        products = new[]
                        {
                            new
                            {
                                id = "erp-001",
                                name = "ERP Product 1",
                                description = "Product from ERP system",
                                price = 29.99m,
                                category = "Electronics",
                                isActive = true
                            },
                            new
                            {
                                id = "erp-002", 
                                name = "ERP Product 2",
                                description = "Another ERP product",
                                price = 49.99m,
                                category = "Books",
                                isActive = true
                            }
                        },
                        total = 2,
                        page = 1,
                        pageSize = 50
                    })));

        // Setup Warehouse service mock responses
        _warehouseMockServer
            .Given(Request.Create().WithPath("/api/inventory").UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(JsonSerializer.Serialize(new
                    {
                        inventory = new[]
                        {
                            new
                            {
                                productId = "erp-001",
                                stockQuantity = 150,
                                inStock = true,
                                warehouseLocation = "A-1-5"
                            },
                            new
                            {
                                productId = "erp-002",
                                stockQuantity = 75,
                                inStock = true,
                                warehouseLocation = "B-2-3"
                            }
                        }
                    })));

        // Setup ERP create product mock
        _erpMockServer
            .Given(Request.Create().WithPath("/api/products").UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(201)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(JsonSerializer.Serialize(new
                    {
                        id = "erp-new-001",
                        name = "{{request.bodyAsJson.name}}",
                        description = "{{request.bodyAsJson.description}}",
                        price = "{{request.bodyAsJson.price}}",
                        category = "{{request.bodyAsJson.category}}",
                        isActive = "{{request.bodyAsJson.isActive}}"
                    }))
                    .WithTransformer());

        // Setup Warehouse create inventory mock
        _warehouseMockServer
            .Given(Request.Create().WithPath("/api/inventory").UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(201)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(JsonSerializer.Serialize(new
                    {
                        productId = "erp-new-001",
                        stockQuantity = 0,
                        inStock = false,
                        warehouseLocation = "PENDING"
                    })));
    }

    #region GetProducts Integration Tests

    [Fact]
    public async Task GetProducts_WithDefaultPagination_ShouldReturnProductsWithTelemetry()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/products");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("Content-Type");

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ProductListResponse>(content, new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true 
        });

        result.Should().NotBeNull();
        result!.Products.Should().NotBeEmpty();
        result.Total.Should().BeGreaterThan(0);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(50);

        // Verify products have warehouse data merged
        result.Products.Should().AllSatisfy(product =>
        {
            product.Id.Should().NotBeNullOrEmpty();
            product.Name.Should().NotBeNullOrEmpty();
            product.Price.Should().BeGreaterThan(0);
            product.Category.Should().NotBeNullOrEmpty();
            product.StockQuantity.Should().BeGreaterOrEqualTo(0);
            product.WarehouseLocation.Should().NotBeNullOrEmpty();
        });
    }

    [Fact]
    public async Task GetProducts_WithCustomPagination_ShouldRespectParameters()
    {
        // Arrange
        const int page = 1;
        const int pageSize = 10;

        // Act
        var response = await _client.GetAsync($"/api/v1/products?page={page}&pageSize={pageSize}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ProductListResponse>(content, new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true 
        });

        result.Should().NotBeNull();
        result!.Page.Should().Be(page);
        result.PageSize.Should().Be(pageSize);
    }

    [Fact]
    public async Task GetProducts_MultipleCalls_ShouldUseCaching()
    {
        // Act - First call
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var response1 = await _client.GetAsync("/api/v1/products");
        stopwatch.Stop();
        var firstCallDuration = stopwatch.ElapsedMilliseconds;

        // Act - Second call (should be cached)
        stopwatch.Restart();
        var response2 = await _client.GetAsync("/api/v1/products");
        stopwatch.Stop();
        var secondCallDuration = stopwatch.ElapsedMilliseconds;

        // Assert
        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);

        var content1 = await response1.Content.ReadAsStringAsync();
        var content2 = await response2.Content.ReadAsStringAsync();

        // Responses should be identical (cached)
        content1.Should().Be(content2);

        // Second call should be faster (cached response)
        secondCallDuration.Should().BeLessThan(firstCallDuration);
    }

    [Fact]
    public async Task GetProducts_WithConcurrentRequests_ShouldHandleLoad()
    {
        // Arrange
        const int concurrentRequests = 10;
        var tasks = new List<Task<HttpResponseMessage>>();

        // Act
        for (int i = 0; i < concurrentRequests; i++)
        {
            tasks.Add(_client.GetAsync("/api/v1/products"));
        }

        var responses = await Task.WhenAll(tasks);

        // Assert
        responses.Should().HaveCount(concurrentRequests);
        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);

        // All responses should have consistent content
        var contents = await Task.WhenAll(responses.Select(r => r.Content.ReadAsStringAsync()));
        contents.Should().OnlyContain(c => !string.IsNullOrEmpty(c));
    }

    #endregion

    #region CreateProduct Integration Tests

    [Fact]
    public async Task CreateProduct_WithValidData_ShouldCreateSuccessfully()
    {
        // Arrange
        var createRequest = new CreateProductRequest
        {
            Name = "Integration Test Product",
            Description = "Product created during integration testing",
            Price = 99.99m,
            Category = "Test Category",
            IsActive = true
        };

        var json = JsonSerializer.Serialize(createRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Add required headers
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Idempotency-Key", $"integration-test-{Guid.NewGuid()}");
        _client.DefaultRequestHeaders.Add("Authorization", "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJ0ZXN0LXVzZXIiLCJuYW1lIjoiVGVzdCBVc2VyIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c");

        // Act
        var response = await _client.PostAsync("/api/v1/products", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var responseContent = await response.Content.ReadAsStringAsync();
        var createdProduct = JsonSerializer.Deserialize<ProductDto>(responseContent, new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true 
        });

        createdProduct.Should().NotBeNull();
        createdProduct!.Id.Should().NotBeNullOrEmpty();
        createdProduct.Name.Should().Be(createRequest.Name);
        createdProduct.Description.Should().Be(createRequest.Description);
        createdProduct.Price.Should().Be(createRequest.Price);
        createdProduct.Category.Should().Be(createRequest.Category);
        createdProduct.IsActive.Should().Be(createRequest.IsActive);
    }

    [Fact]
    public async Task CreateProduct_WithIdempotencyKey_ShouldHandleDuplicates()
    {
        // Arrange
        var createRequest = new CreateProductRequest
        {
            Name = "Idempotent Test Product",
            Description = "Testing idempotency behavior",
            Price = 79.99m,
            Category = "Test Category",
            IsActive = true
        };

        var json = JsonSerializer.Serialize(createRequest);
        var content1 = new StringContent(json, Encoding.UTF8, "application/json");
        var content2 = new StringContent(json, Encoding.UTF8, "application/json");

        var idempotencyKey = $"idempotent-test-{Guid.NewGuid()}";

        // Add headers for first request
        var client1 = _factory.CreateClient();
        client1.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);
        client1.DefaultRequestHeaders.Add("Authorization", "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJ0ZXN0LXVzZXIiLCJuYW1lIjoiVGVzdCBVc2VyIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c");

        // Add headers for second request
        var client2 = _factory.CreateClient();
        client2.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);
        client2.DefaultRequestHeaders.Add("Authorization", "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJ0ZXN0LXVzZXIiLCJuYW1lIjoiVGVzdCBVc2VyIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c");

        // Act - First request
        var response1 = await client1.PostAsync("/api/v1/products", content1);
        
        // Wait a bit to ensure first request is processed
        await Task.Delay(100);
        
        // Act - Duplicate request with same idempotency key
        var response2 = await client2.PostAsync("/api/v1/products", content2);

        // Assert
        response1.StatusCode.Should().Be(HttpStatusCode.Created);
        
        // Second response should either be:
        // - 201 Created (if cached response is returned)
        // - 409 Conflict (if request is still being processed)
        response2.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.Conflict);

        if (response2.StatusCode == HttpStatusCode.Created)
        {
            // If both succeeded, responses should be identical (cached response)
            var content1Result = await response1.Content.ReadAsStringAsync();
            var content2Result = await response2.Content.ReadAsStringAsync();
            
            var product1 = JsonSerializer.Deserialize<ProductDto>(content1Result, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            var product2 = JsonSerializer.Deserialize<ProductDto>(content2Result, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            product1!.Id.Should().Be(product2!.Id);
        }
    }

    [Fact]
    public async Task CreateProduct_WithInvalidData_ShouldReturnValidationErrors()
    {
        // Arrange - Invalid request (missing required fields)
        var invalidRequest = new CreateProductRequest
        {
            Name = "", // Invalid: empty name
            Price = -10, // Invalid: negative price
            Category = "", // Invalid: empty category
            IsActive = true
        };

        var json = JsonSerializer.Serialize(invalidRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Idempotency-Key", $"invalid-test-{Guid.NewGuid()}");
        _client.DefaultRequestHeaders.Add("Authorization", "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJ0ZXN0LXVzZXIiLCJuYW1lIjoiVGVzdCBVc2VyIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c");

        // Act
        var response = await _client.PostAsync("/api/v1/products", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().NotBeNullOrEmpty();
        
        // Should contain validation error information
        responseContent.Should().Contain("validation", "Validation errors should be returned");
    }

    [Fact]
    public async Task CreateProduct_WithoutIdempotencyKey_ShouldReturnBadRequest()
    {
        // Arrange
        var createRequest = new CreateProductRequest
        {
            Name = "No Idempotency Key Product",
            Description = "Testing missing idempotency key",
            Price = 59.99m,
            Category = "Test Category",
            IsActive = true
        };

        var json = JsonSerializer.Serialize(createRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _client.DefaultRequestHeaders.Clear();
        // Deliberately NOT adding Idempotency-Key header
        _client.DefaultRequestHeaders.Add("Authorization", "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJ0ZXN0LXVzZXIiLCJuYW1lIjoiVGVzdCBVc2VyIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c");

        // Act
        var response = await _client.PostAsync("/api/v1/products", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("missing_idempotency_key");
        responseContent.Should().Contain("Idempotency-Key header is required");
    }

    [Fact]
    public async Task CreateProduct_WithoutAuthorization_ShouldReturnUnauthorized()
    {
        // Arrange
        var createRequest = new CreateProductRequest
        {
            Name = "Unauthorized Test Product",
            Description = "Testing unauthorized access",
            Price = 39.99m,
            Category = "Test Category",
            IsActive = true
        };

        var json = JsonSerializer.Serialize(createRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Idempotency-Key", $"unauthorized-test-{Guid.NewGuid()}");
        // Deliberately NOT adding Authorization header

        // Act
        var response = await _client.PostAsync("/api/v1/products", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateProduct_ConcurrentRequests_ShouldHandleLoad()
    {
        // Arrange
        const int concurrentRequests = 5;
        var tasks = new List<Task<HttpResponseMessage>>();

        for (int i = 0; i < concurrentRequests; i++)
        {
            var request = new CreateProductRequest
            {
                Name = $"Concurrent Product {i}",
                Description = $"Product created during concurrent test {i}",
                Price = 10.00m + i,
                Category = "Concurrent Test",
                IsActive = true
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Add("Idempotency-Key", $"concurrent-test-{i}-{Guid.NewGuid()}");
            client.DefaultRequestHeaders.Add("Authorization", "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJ0ZXN0LXVzZXIiLCJuYW1lIjoiVGVzdCBVc2VyIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c");

            tasks.Add(client.PostAsync("/api/v1/products", content));
        }

        // Act
        var responses = await Task.WhenAll(tasks);

        // Assert
        responses.Should().HaveCount(concurrentRequests);
        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Created);

        // All products should have unique IDs
        var productIds = new HashSet<string>();
        foreach (var response in responses)
        {
            var content = await response.Content.ReadAsStringAsync();
            var product = JsonSerializer.Deserialize<ProductDto>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            productIds.Add(product!.Id);
        }

        productIds.Should().HaveCount(concurrentRequests, "All created products should have unique IDs");
    }

    #endregion

    public void Dispose()
    {
        _client?.Dispose();
        _erpMockServer?.Stop();
        _warehouseMockServer?.Stop();
        _erpMockServer?.Dispose();
        _warehouseMockServer?.Dispose();
    }
}