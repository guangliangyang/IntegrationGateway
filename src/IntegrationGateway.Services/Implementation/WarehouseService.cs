using System.Text.Json;
using Microsoft.Extensions.Logging;
using IntegrationGateway.Models.External;
using IntegrationGateway.Services.Interfaces;

namespace IntegrationGateway.Services.Implementation;

public class WarehouseService : IWarehouseService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WarehouseService> _logger;

    public WarehouseService(IHttpClientFactory httpClientFactory, ILogger<WarehouseService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("WarehouseClient");
        _logger = logger;
    }


    public async Task<WarehouseResponse<WarehouseStock>> GetStockAsync(string productId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Getting stock from Warehouse: {ProductId}", productId);
            
            var response = await _httpClient.GetAsync($"/api/stock/{productId}", cancellationToken);

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
            
            var response = await _httpClient.GetAsync($"/api/stock/bulk?{queryString}", cancellationToken);

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