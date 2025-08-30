using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using IntegrationGateway.Models.External;
using IntegrationGateway.Services.Interfaces;
using IntegrationGateway.Models.Exceptions;

namespace IntegrationGateway.Services.Implementation;

public class ErpService : IErpService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ErpService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public ErpService(IHttpClientFactory httpClientFactory, ILogger<ErpService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("ErpClient");
        _logger = logger;
    }


    public async Task<ErpResponse<ErpProduct>> GetProductAsync(string productId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(productId))
            throw new ValidationException("Product ID cannot be null or empty");
            
        return await ExecuteAsync<ErpProduct>(
            async () =>
            {
                _logger.LogDebug("Getting product from ERP: {ProductId}", productId);
                return await _httpClient.GetAsync($"/api/products/{productId}", cancellationToken);
            },
            async response =>
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<ErpProduct>>(json, JsonOptions);
                
                if (apiResponse?.Success == true && apiResponse.Data != null)
                {
                    return apiResponse.Data;
                }
                
                var errorMsg = apiResponse?.ErrorMessage ?? "Unknown error from ERP";
                _logger.LogError("ERP API returned error for product {ProductId}: {ErrorMessage}", productId, errorMsg);
                throw new ExternalServiceException("ERP", errorMsg);
            },
            $"getting product {productId}"
        );
    }

    public async Task<ErpResponse<List<ErpProduct>>> GetProductsAsync(CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync<List<ErpProduct>>(
            async () =>
            {
                _logger.LogDebug("Getting all products from ERP");
                return await _httpClient.GetAsync("/api/products", cancellationToken);
            },
            async response =>
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<List<ErpProduct>>>(json, JsonOptions);
                
                if (apiResponse?.Success == true && apiResponse.Data != null)
                {
                    _logger.LogDebug("Successfully retrieved {Count} products from ERP", apiResponse.Data.Count);
                    return apiResponse.Data;
                }
                
                var errorMsg = apiResponse?.ErrorMessage ?? "Unknown error from ERP";
                _logger.LogError("ERP API returned error: {ErrorMessage}", errorMsg);
                throw new ExternalServiceException("ERP", errorMsg);
            },
            "getting products"
        );
    }

    public async Task<ErpResponse<ErpProduct>> CreateProductAsync(ErpProductRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null)
            throw new ValidationException("Request cannot be null");
            
        return await ExecuteAsync<ErpProduct>(
            async () =>
            {
                _logger.LogDebug("Creating product in ERP: {ProductName}", request.Name);
                
                var json = JsonSerializer.Serialize(request, JsonOptions);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                return await _httpClient.PostAsync("/api/products", content, cancellationToken);
            },
            async response =>
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<ErpProduct>>(json, JsonOptions);
                
                if (apiResponse?.Success == true && apiResponse.Data != null)
                {
                    _logger.LogDebug("Successfully created product in ERP: {ProductId}", apiResponse.Data.Id);
                    return apiResponse.Data;
                }
                
                var errorMsg = apiResponse?.ErrorMessage ?? "Unknown error from ERP";
                _logger.LogError("ERP API returned error creating product {ProductName}: {ErrorMessage}", request.Name, errorMsg);
                throw new ExternalServiceException("ERP", errorMsg);
            },
            $"creating product {request.Name}"
        );
    }

    public async Task<ErpResponse<ErpProduct>> UpdateProductAsync(string productId, ErpProductRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(productId))
            throw new ValidationException("Product ID cannot be null or empty");
        if (request == null)
            throw new ValidationException("Request cannot be null");
            
        return await ExecuteAsync<ErpProduct>(
            async () =>
            {
                _logger.LogDebug("Updating product in ERP: {ProductId}", productId);
                
                var json = JsonSerializer.Serialize(request, JsonOptions);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                return await _httpClient.PutAsync($"/api/products/{productId}", content, cancellationToken);
            },
            async response =>
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<ErpProduct>>(json, JsonOptions);
                
                if (apiResponse?.Success == true && apiResponse.Data != null)
                {
                    _logger.LogDebug("Successfully updated product in ERP: {ProductId}", productId);
                    return apiResponse.Data;
                }
                
                var errorMsg = apiResponse?.ErrorMessage ?? "Unknown error from ERP";
                _logger.LogError("ERP API returned error updating product {ProductId}: {ErrorMessage}", productId, errorMsg);
                throw new ExternalServiceException("ERP", errorMsg);
            },
            $"updating product {productId}"
        );
    }

    public async Task<ErpResponse<bool>> DeleteProductAsync(string productId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(productId))
            throw new ValidationException("Product ID cannot be null or empty");
            
        return await ExecuteAsync<bool>(
            async () =>
            {
                _logger.LogDebug("Deleting product in ERP: {ProductId}", productId);
                return await _httpClient.DeleteAsync($"/api/products/{productId}", cancellationToken);
            },
            async response =>
            {
                _logger.LogDebug("Successfully deleted product in ERP: {ProductId}", productId);
                return true;
            },
            $"deleting product {productId}"
        );
    }

    private async Task<ErpResponse<T>> ExecuteAsync<T>(
        Func<Task<HttpResponseMessage>> httpOperation,
        Func<HttpResponseMessage, Task<T>> successHandler,
        string operationDescription)
    {
        var requestId = Guid.NewGuid().ToString();
        
        try
        {
            using var response = await httpOperation();

            if (response.IsSuccessStatusCode)
            {
                var data = await successHandler(response);
                return new ErpResponse<T>
                {
                    Success = true,
                    Data = data,
                    RequestId = requestId
                };
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new ErpResponse<T>
                {
                    Success = false,
                    ErrorMessage = "Resource not found",
                    RequestId = requestId,
                    StatusCode = 404
                };
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            var errorMessage = $"ERP service error: {response.StatusCode}";
            
            _logger.LogError("ERP service error {Operation}: {StatusCode} - {Content}", 
                operationDescription, response.StatusCode, errorContent);
            
            return new ErpResponse<T>
            {
                Success = false,
                ErrorMessage = errorMessage,
                RequestId = requestId,
                StatusCode = (int)response.StatusCode
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed while {Operation}", operationDescription);
            return new ErpResponse<T>
            {
                Success = false,
                ErrorMessage = "Network error occurred",
                RequestId = requestId
            };
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError(ex, "Request timeout while {Operation}", operationDescription);
            return new ErpResponse<T>
            {
                Success = false,
                ErrorMessage = "Request timeout",
                RequestId = requestId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while {Operation}", operationDescription);
            return new ErpResponse<T>
            {
                Success = false,
                ErrorMessage = "Internal error occurred",
                RequestId = requestId
            };
        }
    }
}