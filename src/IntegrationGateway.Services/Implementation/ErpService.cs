using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using IntegrationGateway.Models.External;
using IntegrationGateway.Services.Interfaces;

namespace IntegrationGateway.Services.Implementation;

public class ErpService : IErpService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ErpService> _logger;

    public ErpService(IHttpClientFactory httpClientFactory, ILogger<ErpService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("ErpClient");
        _logger = logger;
    }


    public async Task<ErpResponse<ErpProduct>> GetProductAsync(string productId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Getting product from ERP: {ProductId}", productId);
            
            var response = await _httpClient.GetAsync($"/api/products/{productId}", cancellationToken);

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
            
            var response = await _httpClient.GetAsync("/api/products", cancellationToken);

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
            
            var response = await _httpClient.PostAsync("/api/products", content, cancellationToken);

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
            
            var response = await _httpClient.PutAsync($"/api/products/{productId}", content, cancellationToken);

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
            
            var response = await _httpClient.DeleteAsync($"/api/products/{productId}", cancellationToken);

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