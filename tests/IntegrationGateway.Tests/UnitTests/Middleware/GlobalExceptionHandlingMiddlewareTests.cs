using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Moq;
using FluentAssertions;
using IntegrationGateway.Api.Middleware;
using IntegrationGateway.Models.Exceptions;

namespace IntegrationGateway.Tests.UnitTests.Middleware;

/// <summary>
/// Unit tests for GlobalExceptionHandlingMiddleware
/// Tests exception to HTTP response mapping and environment-specific behavior
/// </summary>
public class GlobalExceptionHandlingMiddlewareTests
{
    private readonly Mock<RequestDelegate> _mockNext;
    private readonly Mock<ILogger<GlobalExceptionHandlingMiddleware>> _mockLogger;
    private readonly Mock<IWebHostEnvironment> _mockEnvironment;
    private readonly DefaultHttpContext _httpContext;

    public GlobalExceptionHandlingMiddlewareTests()
    {
        _mockNext = new Mock<RequestDelegate>();
        _mockLogger = new Mock<ILogger<GlobalExceptionHandlingMiddleware>>();
        _mockEnvironment = new Mock<IWebHostEnvironment>();
        _httpContext = new DefaultHttpContext();
        _httpContext.Response.Body = new MemoryStream();
    }

    #region ValidationException Tests

    [Fact]
    public async Task HandleException_ValidationException_ShouldReturn400WithProblemDetails()
    {
        // Arrange
        var exception = new ValidationException("Product ID cannot be null or empty");
        var middleware = CreateMiddleware();

        _mockNext.Setup(x => x.Invoke(It.IsAny<HttpContext>()))
            .ThrowsAsync(exception);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.StatusCode.Should().Be(400);
        _httpContext.Response.ContentType.Should().Be("application/json");

        var responseContent = await GetResponseContent();
        var problemDetails = JsonSerializer.Deserialize<JsonElement>(responseContent);

        problemDetails.TryGetProperty("type", out var type).Should().BeTrue();
        type.GetString().Should().Contain("httpstatuses.com/400");

        problemDetails.TryGetProperty("title", out var title).Should().BeTrue();
        title.GetString().Should().Be("Bad Request");

        problemDetails.TryGetProperty("status", out var status).Should().BeTrue();
        status.GetInt32().Should().Be(400);

        problemDetails.TryGetProperty("errorType", out var errorType).Should().BeTrue();
        errorType.GetString().Should().Be("validation_error");

        problemDetails.TryGetProperty("traceId", out var traceId).Should().BeTrue();
        traceId.GetString().Should().NotBeNullOrEmpty();

        problemDetails.TryGetProperty("timestamp", out var timestamp).Should().BeTrue();
    }

    [Fact]
    public async Task HandleException_ValidationExceptionWithErrors_ShouldIncludeErrorsInResponse()
    {
        // Arrange
        var validationErrors = new Dictionary<string, string[]>
        {
            { "Name", new[] { "Name is required", "Name must be at least 3 characters" } },
            { "Price", new[] { "Price must be greater than 0" } }
        };
        var exception = new ValidationException(validationErrors);
        var middleware = CreateMiddleware();

        _mockNext.Setup(x => x.Invoke(It.IsAny<HttpContext>()))
            .ThrowsAsync(exception);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.StatusCode.Should().Be(400);

        var responseContent = await GetResponseContent();
        var problemDetails = JsonSerializer.Deserialize<JsonElement>(responseContent);

        problemDetails.TryGetProperty("errors", out var errors).Should().BeTrue();
        errors.ValueKind.Should().Be(JsonValueKind.Object);

        errors.TryGetProperty("Name", out var nameErrors).Should().BeTrue();
        nameErrors.ValueKind.Should().Be(JsonValueKind.Array);
        nameErrors.GetArrayLength().Should().Be(2);

        errors.TryGetProperty("Price", out var priceErrors).Should().BeTrue();
        priceErrors.ValueKind.Should().Be(JsonValueKind.Array);
        priceErrors.GetArrayLength().Should().Be(1);
    }

    #endregion

    #region UnauthorizedException Tests

    [Fact]
    public async Task HandleException_UnauthorizedException_ShouldReturn401WithProblemDetails()
    {
        // Arrange
        var exception = new UnauthorizedException("Invalid JWT token");
        var middleware = CreateMiddleware();

        _mockNext.Setup(x => x.Invoke(It.IsAny<HttpContext>()))
            .ThrowsAsync(exception);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.StatusCode.Should().Be(401);

        var responseContent = await GetResponseContent();
        var problemDetails = JsonSerializer.Deserialize<JsonElement>(responseContent);

        problemDetails.TryGetProperty("status", out var status).Should().BeTrue();
        status.GetInt32().Should().Be(401);

        problemDetails.TryGetProperty("errorType", out var errorType).Should().BeTrue();
        errorType.GetString().Should().Be("unauthorized");

        problemDetails.TryGetProperty("title", out var title).Should().BeTrue();
        title.GetString().Should().Be("Unauthorized");
    }

    [Fact]
    public async Task HandleException_UnauthorizedException_InProduction_ShouldReturnGenericMessage()
    {
        // Arrange
        var exception = new UnauthorizedException("Detailed security error message");
        var middleware = CreateMiddleware(isDevelopment: false);

        _mockNext.Setup(x => x.Invoke(It.IsAny<HttpContext>()))
            .ThrowsAsync(exception);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        var responseContent = await GetResponseContent();
        var problemDetails = JsonSerializer.Deserialize<JsonElement>(responseContent);

        problemDetails.TryGetProperty("detail", out var detail).Should().BeTrue();
        detail.GetString().Should().Be("Authentication required");
    }

    [Fact]
    public async Task HandleException_UnauthorizedException_InDevelopment_ShouldReturnDetailedMessage()
    {
        // Arrange
        var exception = new UnauthorizedException("Invalid JWT token signature");
        var middleware = CreateMiddleware(isDevelopment: true);

        _mockNext.Setup(x => x.Invoke(It.IsAny<HttpContext>()))
            .ThrowsAsync(exception);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        var responseContent = await GetResponseContent();
        var problemDetails = JsonSerializer.Deserialize<JsonElement>(responseContent);

        problemDetails.TryGetProperty("detail", out var detail).Should().BeTrue();
        detail.GetString().Should().Be("Invalid JWT token signature");
    }

    #endregion

    #region NotFoundException Tests

    [Fact]
    public async Task HandleException_NotFoundException_ShouldReturn404WithProblemDetails()
    {
        // Arrange
        var exception = new NotFoundException("Product", "test-product-123");
        var middleware = CreateMiddleware();

        _mockNext.Setup(x => x.Invoke(It.IsAny<HttpContext>()))
            .ThrowsAsync(exception);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.StatusCode.Should().Be(404);

        var responseContent = await GetResponseContent();
        var problemDetails = JsonSerializer.Deserialize<JsonElement>(responseContent);

        problemDetails.TryGetProperty("status", out var status).Should().BeTrue();
        status.GetInt32().Should().Be(404);

        problemDetails.TryGetProperty("errorType", out var errorType).Should().BeTrue();
        errorType.GetString().Should().Be("not_found");

        problemDetails.TryGetProperty("detail", out var detail).Should().BeTrue();
        detail.GetString().Should().Be("Entity \"Product\" (test-product-123) was not found.");
    }

    #endregion

    #region ExternalServiceException Tests

    [Fact]
    public async Task HandleException_ExternalServiceException_ShouldReturn502WithProblemDetails()
    {
        // Arrange
        var exception = new ExternalServiceException("ERP", "Database connection timeout");
        var middleware = CreateMiddleware();

        _mockNext.Setup(x => x.Invoke(It.IsAny<HttpContext>()))
            .ThrowsAsync(exception);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.StatusCode.Should().Be(502);

        var responseContent = await GetResponseContent();
        var problemDetails = JsonSerializer.Deserialize<JsonElement>(responseContent);

        problemDetails.TryGetProperty("status", out var status).Should().BeTrue();
        status.GetInt32().Should().Be(502);

        problemDetails.TryGetProperty("errorType", out var errorType).Should().BeTrue();
        errorType.GetString().Should().Be("external_service_error");

        problemDetails.TryGetProperty("title", out var title).Should().BeTrue();
        title.GetString().Should().Be("Bad Gateway");
    }

    [Fact]
    public async Task HandleException_ExternalServiceException_InProduction_ShouldReturnGenericMessage()
    {
        // Arrange
        var exception = new ExternalServiceException("ERP", "Internal database schema error");
        var middleware = CreateMiddleware(isDevelopment: false);

        _mockNext.Setup(x => x.Invoke(It.IsAny<HttpContext>()))
            .ThrowsAsync(exception);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        var responseContent = await GetResponseContent();
        var problemDetails = JsonSerializer.Deserialize<JsonElement>(responseContent);

        problemDetails.TryGetProperty("detail", out var detail).Should().BeTrue();
        detail.GetString().Should().Be("External service temporarily unavailable");
    }

    [Fact]
    public async Task HandleException_ExternalServiceException_InDevelopment_ShouldReturnDetailedMessage()
    {
        // Arrange
        var exception = new ExternalServiceException("ERP", "Connection string invalid");
        var middleware = CreateMiddleware(isDevelopment: true);

        _mockNext.Setup(x => x.Invoke(It.IsAny<HttpContext>()))
            .ThrowsAsync(exception);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        var responseContent = await GetResponseContent();
        var problemDetails = JsonSerializer.Deserialize<JsonElement>(responseContent);

        problemDetails.TryGetProperty("detail", out var detail).Should().BeTrue();
        detail.GetString().Should().Be("External service 'ERP' error: Connection string invalid");
    }

    #endregion

    #region TaskCanceledException Tests

    [Fact]
    public async Task HandleException_TaskCanceledException_ShouldReturn408WithProblemDetails()
    {
        // Arrange
        var exception = new TaskCanceledException("Request timed out");
        var middleware = CreateMiddleware();

        _mockNext.Setup(x => x.Invoke(It.IsAny<HttpContext>()))
            .ThrowsAsync(exception);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.StatusCode.Should().Be(408);

        var responseContent = await GetResponseContent();
        var problemDetails = JsonSerializer.Deserialize<JsonElement>(responseContent);

        problemDetails.TryGetProperty("status", out var status).Should().BeTrue();
        status.GetInt32().Should().Be(408);

        problemDetails.TryGetProperty("errorType", out var errorType).Should().BeTrue();
        errorType.GetString().Should().Be("request_timeout");

        problemDetails.TryGetProperty("title", out var title).Should().BeTrue();
        title.GetString().Should().Be("Request Timeout");
    }

    #endregion

    #region ArgumentException Tests

    [Fact]
    public async Task HandleException_ArgumentException_ShouldReturn400WithProblemDetails()
    {
        // Arrange
        var exception = new ArgumentException("Invalid parameter value", "productId");
        var middleware = CreateMiddleware();

        _mockNext.Setup(x => x.Invoke(It.IsAny<HttpContext>()))
            .ThrowsAsync(exception);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.StatusCode.Should().Be(400);

        var responseContent = await GetResponseContent();
        var problemDetails = JsonSerializer.Deserialize<JsonElement>(responseContent);

        problemDetails.TryGetProperty("errorType", out var errorType).Should().BeTrue();
        errorType.GetString().Should().Be("bad_request");
    }

    #endregion

    #region Generic Exception Tests

    [Fact]
    public async Task HandleException_GenericException_ShouldReturn500WithProblemDetails()
    {
        // Arrange
        var exception = new InvalidOperationException("Unexpected system error");
        var middleware = CreateMiddleware();

        _mockNext.Setup(x => x.Invoke(It.IsAny<HttpContext>()))
            .ThrowsAsync(exception);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.StatusCode.Should().Be(500);

        var responseContent = await GetResponseContent();
        var problemDetails = JsonSerializer.Deserialize<JsonElement>(responseContent);

        problemDetails.TryGetProperty("status", out var status).Should().BeTrue();
        status.GetInt32().Should().Be(500);

        problemDetails.TryGetProperty("errorType", out var errorType).Should().BeTrue();
        errorType.GetString().Should().Be("internal_server_error");

        problemDetails.TryGetProperty("title", out var title).Should().BeTrue();
        title.GetString().Should().Be("Internal Server Error");
    }

    [Fact]
    public async Task HandleException_GenericException_InProduction_ShouldHideDetails()
    {
        // Arrange
        var exception = new InvalidOperationException("Sensitive internal error details");
        var middleware = CreateMiddleware(isDevelopment: false);

        _mockNext.Setup(x => x.Invoke(It.IsAny<HttpContext>()))
            .ThrowsAsync(exception);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        var responseContent = await GetResponseContent();
        var problemDetails = JsonSerializer.Deserialize<JsonElement>(responseContent);

        problemDetails.TryGetProperty("detail", out var detail).Should().BeTrue();
        detail.GetString().Should().Be("An unexpected error occurred");

        problemDetails.TryGetProperty("stackTrace", out var stackTrace).Should().BeTrue();
        stackTrace.ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task HandleException_GenericException_InDevelopment_ShouldShowDetails()
    {
        // Arrange
        var exception = new InvalidOperationException("Debug information here");
        var middleware = CreateMiddleware(isDevelopment: true);

        _mockNext.Setup(x => x.Invoke(It.IsAny<HttpContext>()))
            .ThrowsAsync(exception);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        var responseContent = await GetResponseContent();
        var problemDetails = JsonSerializer.Deserialize<JsonElement>(responseContent);

        problemDetails.TryGetProperty("detail", out var detail).Should().BeTrue();
        detail.GetString().Should().Be("Debug information here");

        problemDetails.TryGetProperty("stackTrace", out var stackTrace).Should().BeTrue();
        stackTrace.ValueKind.Should().Be(JsonValueKind.String);
        stackTrace.GetString().Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Logging Tests

    [Fact]
    public async Task HandleException_ValidationException_ShouldLogAsWarning()
    {
        // Arrange
        var exception = new ValidationException("Test validation error");
        var middleware = CreateMiddleware();

        _mockNext.Setup(x => x.Invoke(It.IsAny<HttpContext>()))
            .ThrowsAsync(exception);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("ValidationException")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleException_InternalServerError_ShouldLogAsError()
    {
        // Arrange
        var exception = new InvalidOperationException("System failure");
        var middleware = CreateMiddleware();

        _mockNext.Setup(x => x.Invoke(It.IsAny<HttpContext>()))
            .ThrowsAsync(exception);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("InvalidOperationException")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Helper Methods

    private GlobalExceptionHandlingMiddleware CreateMiddleware(bool isDevelopment = false)
    {
        _mockEnvironment.Setup(x => x.IsDevelopment()).Returns(isDevelopment);
        _mockEnvironment.Setup(x => x.EnvironmentName)
            .Returns(isDevelopment ? Environments.Development : Environments.Production);

        return new GlobalExceptionHandlingMiddleware(
            _mockNext.Object,
            _mockLogger.Object,
            _mockEnvironment.Object);
    }

    private async Task<string> GetResponseContent()
    {
        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(_httpContext.Response.Body);
        return await reader.ReadToEndAsync();
    }

    #endregion
}