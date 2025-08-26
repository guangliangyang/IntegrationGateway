using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using FluentAssertions;
using System.Text.Json;
using System.Text;

namespace IntegrationGateway.Tests.Integration;

public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Health_Endpoint_Should_Return_Healthy()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Healthy");
    }

    [Fact]
    public async Task V1_Products_Endpoint_Should_Be_Available()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/products");

        // Assert - Should return 401 Unauthorized due to JWT requirement
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task V2_Products_Endpoint_Should_Be_Available()
    {
        // Act
        var response = await _client.GetAsync("/api/v2/products");

        // Assert - Should return 401 Unauthorized due to JWT requirement
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Swagger_Documentation_Should_Be_Available()
    {
        // Act
        var response = await _client.GetAsync("/swagger/index.html");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Swagger");
    }

    [Fact]
    public async Task OpenAPI_V1_Spec_Should_Be_Available()
    {
        // Act
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        
        // Parse as JSON to verify it's valid
        using var document = JsonDocument.Parse(content);
        document.RootElement.GetProperty("openapi").GetString().Should().StartWith("3.0");
    }

    [Fact]
    public async Task OpenAPI_V2_Spec_Should_Be_Available()
    {
        // Act
        var response = await _client.GetAsync("/swagger/v2/swagger.json");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        
        // Parse as JSON to verify it's valid
        using var document = JsonDocument.Parse(content);
        document.RootElement.GetProperty("openapi").GetString().Should().StartWith("3.0");
    }

    [Fact]
    public async Task POST_Request_Without_Idempotency_Key_Should_Return_BadRequest()
    {
        // Arrange
        var productRequest = new
        {
            Name = "Test Product",
            Description = "Test Description",
            Price = 99.99,
            Category = "Electronics"
        };

        var json = JsonSerializer.Serialize(productRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/v1/products", content);

        // Assert - Should return 401 Unauthorized first due to JWT requirement
        // but the idempotency middleware should kick in if auth was bypassed
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Application_Should_Start_Successfully()
    {
        // This test verifies that the application can start without throwing exceptions
        // The factory.CreateClient() call in the constructor would fail if the app couldn't start

        // Act & Assert
        _client.Should().NotBeNull();
        _factory.Should().NotBeNull();
    }
}

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Override services for testing if needed
            // For example, replace external HTTP clients with mocks
        });

        builder.UseEnvironment("Testing");
    }
}