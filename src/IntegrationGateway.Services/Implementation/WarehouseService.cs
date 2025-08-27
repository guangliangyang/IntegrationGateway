using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using IntegrationGateway.Models.External;
using IntegrationGateway.Services.Configuration;
using IntegrationGateway.Services.Interfaces;

namespace IntegrationGateway.Services.Implementation;

public class WarehouseService : IWarehouseService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WarehouseService> _logger;
    private readonly WarehouseServiceOptions _options;
    private readonly ResiliencePipeline<HttpResponseMessage> _resiliencePipeline;

    public WarehouseService(HttpClient httpClient, ILogger<WarehouseService> logger, IOptions<WarehouseServiceOptions> options)
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
                Delay = TimeSpan.FromMilliseconds(500),
                MaxRetryAttempts = _options.MaxRetries,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                OnRetry = args =>
                {
                    _logger.LogWarning("Warehouse Service retry {AttemptNumber} after {Delay}ms. Outcome: {Outcome}",
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
                SamplingDuration = TimeSpan.FromSeconds(20),
                MinimumThroughput = 3,
                BreakDuration = TimeSpan.FromSeconds(30),
                OnOpened = args =>
                {
                    _logger.LogError("Warehouse Service circuit breaker opened. Outcome: {Outcome}", args.Outcome);
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    _logger.LogInformation("Warehouse Service circuit breaker closed");
                    return ValueTask.CompletedTask;
                }
            })
            .AddTimeout(TimeSpan.FromSeconds(_options.TimeoutSeconds))
            .Build();
    }

    public async Task<WarehouseResponse<WarehouseStock>> GetStockAsync(string productId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Getting stock from Warehouse: {ProductId}", productId);
            
            var response = await _resiliencePipeline.ExecuteAsync(async token =>
            {
                return await _httpClient.GetAsync($"/api/stock/{productId}", token);
            }, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var stock = JsonSerializer.Deserialize<WarehouseStock>(json, GetJsonOptions());
                
                _logger.LogDebug("Successfully retrieved stock from Warehouse: {ProductId}, Quantity: {Quantity}", 
                    productId, stock?.Quantity);
                
                return new WarehouseResponse<WarehouseStock>
                {
                    Success = true,
                    Data = stock,
                    RequestId = Guid.NewGuid().ToString()
                };
            }

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogDebug("Stock not found in Warehouse: {ProductId}", productId);
                
                // Return default stock for non-existent products
                return new WarehouseResponse<WarehouseStock>
                {
                    Success = true,
                    Data = new WarehouseStock
                    {
                        ProductId = productId,
                        Quantity = 0,
                        InStock = false,
                        LastUpdated = DateTime.UtcNow
                    },
                    RequestId = Guid.NewGuid().ToString()
                };
            }

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Warehouse service error for stock {ProductId}: {StatusCode} - {Content}", 
                productId, response.StatusCode, errorContent);
            
            return new WarehouseResponse<WarehouseStock>
            {
                Success = false,
                ErrorMessage = $"Warehouse service error: {response.StatusCode}",
                RequestId = Guid.NewGuid().ToString()
            };
        }
        catch (BrokenCircuitException)
        {
            _logger.LogWarning("Warehouse service circuit breaker is open for stock {ProductId}, returning default", productId);
            
            // Return default stock when service is unavailable
            return new WarehouseResponse<WarehouseStock>
            {
                Success = true,
                Data = new WarehouseStock
                {
                    ProductId = productId,
                    Quantity = 0,
                    InStock = false,
                    LastUpdated = DateTime.UtcNow
                },
                RequestId = Guid.NewGuid().ToString()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting stock from Warehouse: {ProductId}", productId);
            
            // Return default stock on errors for graceful degradation
            return new WarehouseResponse<WarehouseStock>
            {
                Success = true,
                Data = new WarehouseStock
                {
                    ProductId = productId,
                    Quantity = 0,
                    InStock = false,
                    LastUpdated = DateTime.UtcNow
                },
                RequestId = Guid.NewGuid().ToString()
            };
        }
    }

    public async Task<WarehouseResponse<BulkStockResponse>> GetBulkStockAsync(List<string> productIds, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Getting bulk stock from Warehouse: {ProductCount} products", productIds.Count);
            
            var queryString = string.Join("&", productIds.Select(id => $"productIds={Uri.EscapeDataString(id)}"));
            
            var response = await _resiliencePipeline.ExecuteAsync(async token =>
            {
                return await _httpClient.GetAsync($"/api/stock/bulk?{queryString}", token);
            }, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var bulkResponse = JsonSerializer.Deserialize<BulkStockResponse>(json, GetJsonOptions()) 
                                   ?? new BulkStockResponse();
                
                _logger.LogDebug("Successfully retrieved bulk stock from Warehouse: {FoundCount} found, {NotFoundCount} not found", 
                    bulkResponse.Stocks.Count, bulkResponse.NotFound.Count);
                
                return new WarehouseResponse<BulkStockResponse>
                {
                    Success = true,
                    Data = bulkResponse,
                    RequestId = Guid.NewGuid().ToString()
                };
            }

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Warehouse service error for bulk stock: {StatusCode} - {Content}", 
                response.StatusCode, errorContent);
            
            return new WarehouseResponse<BulkStockResponse>
            {
                Success = false,
                ErrorMessage = $"Warehouse service error: {response.StatusCode}",
                RequestId = Guid.NewGuid().ToString()
            };
        }
        catch (BrokenCircuitException)
        {
            _logger.LogWarning("Warehouse service circuit breaker is open for bulk stock, returning defaults");
            
            // Return default stock for all products when service is unavailable
            var defaultStocks = productIds.Select(id => new WarehouseStock
            {
                ProductId = id,
                Quantity = 0,
                InStock = false,
                LastUpdated = DateTime.UtcNow
            }).ToList();
            
            return new WarehouseResponse<BulkStockResponse>
            {
                Success = true,
                Data = new BulkStockResponse { Stocks = defaultStocks },
                RequestId = Guid.NewGuid().ToString()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting bulk stock from Warehouse");
            
            // Return default stock for all products on errors
            var defaultStocks = productIds.Select(id => new WarehouseStock
            {
                ProductId = id,
                Quantity = 0,
                InStock = false,
                LastUpdated = DateTime.UtcNow
            }).ToList();
            
            return new WarehouseResponse<BulkStockResponse>
            {
                Success = true,
                Data = new BulkStockResponse { Stocks = defaultStocks },
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