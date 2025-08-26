using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;
using IntegrationGateway.Models.External;
using IntegrationGateway.Services.Configuration;
using IntegrationGateway.Services.Interfaces;

namespace IntegrationGateway.Services.Implementation;

public class ErpService : IErpService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ErpService> _logger;
    private readonly ErpServiceOptions _options;
    private readonly ResiliencePipeline<HttpResponseMessage> _resiliencePipeline;

    public ErpService(HttpClient httpClient, ILogger<ErpService> logger, IOptions<ErpServiceOptions> options)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
        
        ConfigureHttpClient();
        _resiliencePipeline = CreateResiliencePipeline();
    }

    private void ConfigureHttpClient()
    {
        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
        
        if (!string.IsNullOrEmpty(_options.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", _options.ApiKey);
        }
        
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "IntegrationGateway/1.0");
    }

    private ResiliencePipeline<HttpResponseMessage> CreateResiliencePipeline()
    {
        return new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new()
            {
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .HandleResult(response => !response.IsSuccessStatusCode)
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>(),
                Delay = TimeSpan.FromSeconds(1),
                MaxRetryAttempts = _options.MaxRetries,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                OnRetry = args =>
                {
                    _logger.LogWarning("ERP Service retry {AttemptNumber} after {Delay}ms. Outcome: {Outcome}",
                        args.AttemptNumber, args.RetryDelay.TotalMilliseconds, args.Outcome);
                    return ValueTask.CompletedTask;
                }
            })
            .AddCircuitBreaker(new()
            {
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .HandleResult(response => !response.IsSuccessStatusCode)
                    .Handle<HttpRequestException>(),
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 5,
                BreakDuration = TimeSpan.FromMinutes(1),
                OnOpened = args =>
                {
                    _logger.LogError("ERP Service circuit breaker opened. Outcome: {Outcome}", args.Outcome);
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    _logger.LogInformation("ERP Service circuit breaker closed");
                    return ValueTask.CompletedTask;
                }
            })
            .AddTimeout(TimeSpan.FromSeconds(_options.TimeoutSeconds))
            .Build();
    }

    public async Task<ErpResponse<ErpProduct>> GetProductAsync(string productId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Getting product from ERP: {ProductId}", productId);
            
            var response = await _resiliencePipeline.ExecuteAsync(async token =>
            {
                return await _httpClient.GetAsync($"/api/products/{productId}", token);
            }, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var product = JsonSerializer.Deserialize<ErpProduct>(json, GetJsonOptions());
                
                _logger.LogDebug("Successfully retrieved product from ERP: {ProductId}", productId);
                
                return new ErpResponse<ErpProduct>
                {
                    Success = true,
                    Data = product,
                    RequestId = Guid.NewGuid().ToString()
                };
            }

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new ErpResponse<ErpProduct>
                {
                    Success = false,
                    ErrorMessage = "Product not found",
                    RequestId = Guid.NewGuid().ToString()
                };
            }

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("ERP service error for product {ProductId}: {StatusCode} - {Content}", 
                productId, response.StatusCode, errorContent);
            
            return new ErpResponse<ErpProduct>
            {
                Success = false,
                ErrorMessage = $"ERP service error: {response.StatusCode}",
                RequestId = Guid.NewGuid().ToString()
            };
        }
        catch (CircuitBreakerRejectedException)
        {
            _logger.LogError("ERP service circuit breaker is open for product {ProductId}", productId);
            return new ErpResponse<ErpProduct>
            {
                Success = false,
                ErrorMessage = "ERP service is temporarily unavailable",
                RequestId = Guid.NewGuid().ToString()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting product from ERP: {ProductId}", productId);
            return new ErpResponse<ErpProduct>
            {
                Success = false,
                ErrorMessage = "Internal error occurred",
                RequestId = Guid.NewGuid().ToString()
            };
        }
    }

    public async Task<ErpResponse<List<ErpProduct>>> GetProductsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Getting all products from ERP");
            
            var response = await _resiliencePipeline.ExecuteAsync(async token =>
            {
                return await _httpClient.GetAsync("/api/products", token);
            }, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var products = JsonSerializer.Deserialize<List<ErpProduct>>(json, GetJsonOptions()) ?? new List<ErpProduct>();
                
                _logger.LogDebug("Successfully retrieved {Count} products from ERP", products.Count);
                
                return new ErpResponse<List<ErpProduct>>
                {
                    Success = true,
                    Data = products,
                    RequestId = Guid.NewGuid().ToString()
                };
            }

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("ERP service error getting products: {StatusCode} - {Content}", 
                response.StatusCode, errorContent);
            
            return new ErpResponse<List<ErpProduct>>
            {
                Success = false,
                ErrorMessage = $"ERP service error: {response.StatusCode}",
                RequestId = Guid.NewGuid().ToString()
            };
        }
        catch (CircuitBreakerRejectedException)
        {
            _logger.LogError("ERP service circuit breaker is open for products list");
            return new ErpResponse<List<ErpProduct>>
            {
                Success = false,
                ErrorMessage = "ERP service is temporarily unavailable",
                RequestId = Guid.NewGuid().ToString()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting products from ERP");
            return new ErpResponse<List<ErpProduct>>
            {
                Success = false,
                ErrorMessage = "Internal error occurred",
                RequestId = Guid.NewGuid().ToString()
            };
        }
    }

    public async Task<ErpResponse<ErpProduct>> CreateProductAsync(ErpProductRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Creating product in ERP: {ProductName}", request.Name);
            
            var json = JsonSerializer.Serialize(request, GetJsonOptions());
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _resiliencePipeline.ExecuteAsync(async token =>
            {
                return await _httpClient.PostAsync("/api/products", content, token);
            }, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                var product = JsonSerializer.Deserialize<ErpProduct>(responseJson, GetJsonOptions());
                
                _logger.LogDebug("Successfully created product in ERP: {ProductId}", product?.Id);
                
                return new ErpResponse<ErpProduct>
                {
                    Success = true,
                    Data = product,
                    RequestId = Guid.NewGuid().ToString()
                };
            }

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("ERP service error creating product {ProductName}: {StatusCode} - {Content}", 
                request.Name, response.StatusCode, errorContent);
            
            return new ErpResponse<ErpProduct>
            {
                Success = false,
                ErrorMessage = $"ERP service error: {response.StatusCode}",
                RequestId = Guid.NewGuid().ToString()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating product in ERP: {ProductName}", request.Name);
            return new ErpResponse<ErpProduct>
            {
                Success = false,
                ErrorMessage = "Internal error occurred",
                RequestId = Guid.NewGuid().ToString()
            };
        }
    }

    public async Task<ErpResponse<ErpProduct>> UpdateProductAsync(string productId, ErpProductRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Updating product in ERP: {ProductId}", productId);
            
            var json = JsonSerializer.Serialize(request, GetJsonOptions());
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _resiliencePipeline.ExecuteAsync(async token =>
            {
                return await _httpClient.PutAsync($"/api/products/{productId}", content, token);
            }, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                var product = JsonSerializer.Deserialize<ErpProduct>(responseJson, GetJsonOptions());
                
                _logger.LogDebug("Successfully updated product in ERP: {ProductId}", productId);
                
                return new ErpResponse<ErpProduct>
                {
                    Success = true,
                    Data = product,
                    RequestId = Guid.NewGuid().ToString()
                };
            }

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new ErpResponse<ErpProduct>
                {
                    Success = false,
                    ErrorMessage = "Product not found",
                    RequestId = Guid.NewGuid().ToString()
                };
            }

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("ERP service error updating product {ProductId}: {StatusCode} - {Content}", 
                productId, response.StatusCode, errorContent);
            
            return new ErpResponse<ErpProduct>
            {
                Success = false,
                ErrorMessage = $"ERP service error: {response.StatusCode}",
                RequestId = Guid.NewGuid().ToString()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error updating product in ERP: {ProductId}", productId);
            return new ErpResponse<ErpProduct>
            {
                Success = false,
                ErrorMessage = "Internal error occurred",
                RequestId = Guid.NewGuid().ToString()
            };
        }
    }

    public async Task<ErpResponse<bool>> DeleteProductAsync(string productId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Deleting product in ERP: {ProductId}", productId);
            
            var response = await _resiliencePipeline.ExecuteAsync(async token =>
            {
                return await _httpClient.DeleteAsync($"/api/products/{productId}", token);
            }, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Successfully deleted product in ERP: {ProductId}", productId);
                
                return new ErpResponse<bool>
                {
                    Success = true,
                    Data = true,
                    RequestId = Guid.NewGuid().ToString()
                };
            }

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new ErpResponse<bool>
                {
                    Success = false,
                    ErrorMessage = "Product not found",
                    RequestId = Guid.NewGuid().ToString()
                };
            }

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("ERP service error deleting product {ProductId}: {StatusCode} - {Content}", 
                productId, response.StatusCode, errorContent);
            
            return new ErpResponse<bool>
            {
                Success = false,
                ErrorMessage = $"ERP service error: {response.StatusCode}",
                RequestId = Guid.NewGuid().ToString()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting product in ERP: {ProductId}", productId);
            return new ErpResponse<bool>
            {
                Success = false,
                ErrorMessage = "Internal error occurred",
                RequestId = Guid.NewGuid().ToString()
            };
        }
    }

    private static JsonSerializerOptions GetJsonOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }
}