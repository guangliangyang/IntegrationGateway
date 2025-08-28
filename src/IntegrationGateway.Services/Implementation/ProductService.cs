using Microsoft.Extensions.Logging;
using IntegrationGateway.Models;
using IntegrationGateway.Models.DTOs;
using IntegrationGateway.Models.External;
using IntegrationGateway.Models.Common;
using IntegrationGateway.Services.Interfaces;

namespace IntegrationGateway.Services.Implementation;

public class ProductService : IProductService
{
    private readonly IErpService _erpService;
    private readonly IWarehouseService _warehouseService;
    private readonly IIdempotencyService _idempotencyService;
    private readonly ILogger<ProductService> _logger;

    public ProductService(
        IErpService erpService,
        IWarehouseService warehouseService,
        IIdempotencyService idempotencyService,
        ILogger<ProductService> logger)
    {
        _erpService = erpService;
        _warehouseService = warehouseService;
        _idempotencyService = idempotencyService;
        _logger = logger;
    }

    public async Task<ProductListResponse> GetProductsAsync(int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting products list, page: {Page}, size: {PageSize}", page, pageSize);

        try
        {
            // Get products from ERP
            var erpResponse = await _erpService.GetProductsAsync(cancellationToken);
            if (!erpResponse.Success || erpResponse.Data == null)
            {
                _logger.LogError("Failed to get products from ERP: {ErrorMessage}", erpResponse.ErrorMessage);
                return new ProductListResponse { Products = new List<ProductDto>() };
            }

            var erpProducts = erpResponse.Data;
            _logger.LogDebug("Retrieved {Count} products from ERP", erpProducts.Count);

            // Get stock information in bulk
            var productIds = erpProducts.Select(p => p.Id).ToList();
            var warehouseResponse = await _warehouseService.GetBulkStockAsync(productIds, cancellationToken);
            
            var stockLookup = new Dictionary<string, WarehouseStock>();
            if (warehouseResponse.Success && warehouseResponse.Data != null)
            {
                stockLookup = warehouseResponse.Data.Stocks.ToDictionary(s => s.ProductId, s => s);
                _logger.LogDebug("Retrieved stock for {Count} products from Warehouse", stockLookup.Count);
            }
            else
            {
                _logger.LogWarning("Failed to get stock from Warehouse: {ErrorMessage}", warehouseResponse.ErrorMessage);
            }

            // Merge ERP and Warehouse data
            var mergedProducts = erpProducts.Select(erpProduct =>
            {
                stockLookup.TryGetValue(erpProduct.Id, out var stock);
                return MapToProductDto(erpProduct, stock);
            }).ToList();

            // Apply pagination
            var paginatedProducts = mergedProducts
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var response = new ProductListResponse
            {
                Products = paginatedProducts,
                Total = mergedProducts.Count,
                Page = page,
                PageSize = pageSize
            };

            _logger.LogDebug("Returning {Count} products for page {Page}", paginatedProducts.Count, page);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting products list");
            return new ProductListResponse { Products = new List<ProductDto>() };
        }
    }

    public async Task<ProductListV2Response> GetProductsV2Async(int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting products list V2, page: {Page}, size: {PageSize}", page, pageSize);

        try
        {
            // Get products from ERP
            var erpResponse = await _erpService.GetProductsAsync(cancellationToken);
            if (!erpResponse.Success || erpResponse.Data == null)
            {
                _logger.LogError("Failed to get products from ERP: {ErrorMessage}", erpResponse.ErrorMessage);
                return new ProductListV2Response { Products = new List<ProductV2Dto>() };
            }

            var erpProducts = erpResponse.Data;
            _logger.LogDebug("Retrieved {Count} products from ERP for V2", erpProducts.Count);

            // Get stock information in bulk
            var productIds = erpProducts.Select(p => p.Id).ToList();
            var warehouseResponse = await _warehouseService.GetBulkStockAsync(productIds, cancellationToken);
            
            var stockLookup = new Dictionary<string, WarehouseStock>();
            if (warehouseResponse.Success && warehouseResponse.Data != null)
            {
                stockLookup = warehouseResponse.Data.Stocks.ToDictionary(s => s.ProductId, s => s);
                _logger.LogDebug("Retrieved stock for {Count} products from Warehouse for V2", stockLookup.Count);
            }

            // Merge ERP and Warehouse data with V2 mapping
            var mergedProducts = erpProducts.Select(erpProduct =>
            {
                stockLookup.TryGetValue(erpProduct.Id, out var stock);
                return MapToProductV2Dto(erpProduct, stock);
            }).ToList();

            // Apply pagination
            var paginatedProducts = mergedProducts
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var response = new ProductListV2Response
            {
                Products = paginatedProducts,
                Total = mergedProducts.Count,
                Page = page,
                PageSize = pageSize
            };

            _logger.LogDebug("Returning {Count} products V2 for page {Page}", paginatedProducts.Count, page);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting products list V2");
            return new ProductListV2Response { Products = new List<ProductV2Dto>() };
        }
    }

    public async Task<ProductDto?> GetProductAsync(string productId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting product by ID: {ProductId}", productId);

        try
        {
            // Get product from ERP
            var erpResponse = await _erpService.GetProductAsync(productId, cancellationToken);
            if (!erpResponse.Success || erpResponse.Data == null)
            {
                _logger.LogWarning("Product {ProductId} not found in ERP", productId);
                return null;
            }

            var erpProduct = erpResponse.Data;
            _logger.LogDebug("Retrieved product {ProductId} from ERP", productId);

            // Get stock information
            var warehouseResponse = await _warehouseService.GetStockAsync(productId, cancellationToken);
            WarehouseStock? stock = null;
            
            if (warehouseResponse.Success && warehouseResponse.Data != null)
            {
                stock = warehouseResponse.Data;
                _logger.LogDebug("Retrieved stock for product {ProductId} from Warehouse", productId);
            }
            else
            {
                _logger.LogWarning("Failed to get stock for product {ProductId}: {ErrorMessage}", 
                    productId, warehouseResponse.ErrorMessage);
            }

            var productDto = MapToProductDto(erpProduct, stock);
            _logger.LogDebug("Returning product {ProductId}", productId);
            
            return productDto;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting product {ProductId}", productId);
            return null;
        }
    }

    public async Task<ProductV2Dto?> GetProductV2Async(string productId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting product by ID V2: {ProductId}", productId);

        try
        {
            // Get product from ERP
            var erpResponse = await _erpService.GetProductAsync(productId, cancellationToken);
            if (!erpResponse.Success || erpResponse.Data == null)
            {
                _logger.LogWarning("Product {ProductId} not found in ERP for V2", productId);
                return null;
            }

            var erpProduct = erpResponse.Data;
            _logger.LogDebug("Retrieved product {ProductId} from ERP for V2", productId);

            // Get stock information
            var warehouseResponse = await _warehouseService.GetStockAsync(productId, cancellationToken);
            WarehouseStock? stock = null;
            
            if (warehouseResponse.Success && warehouseResponse.Data != null)
            {
                stock = warehouseResponse.Data;
                _logger.LogDebug("Retrieved stock for product {ProductId} from Warehouse for V2", productId);
            }

            var productV2Dto = MapToProductV2Dto(erpProduct, stock);
            _logger.LogDebug("Returning product V2 {ProductId}", productId);
            
            return productV2Dto;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting product V2 {ProductId}", productId);
            return null;
        }
    }

    public async Task<ProductDto> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Creating product: {Name}", request.Name);

        try
        {
            var createRequest = new ErpProductRequest
            {
                Name = request.Name,
                Description = request.Description,
                Price = request.Price,
                Category = request.Category,
                IsActive = request.IsActive
            };

            var erpResponse = await _erpService.CreateProductAsync(createRequest, cancellationToken);
            if (!erpResponse.Success || erpResponse.Data == null)
            {
                throw new InvalidOperationException($"Failed to create product: {erpResponse.ErrorMessage}");
            }

            var createdProduct = erpResponse.Data;
            _logger.LogDebug("Created product {ProductId} in ERP", createdProduct.Id);

            // Note: Cache invalidation is now handled by MediatR CacheInvalidationBehaviour
            
            return MapToProductDto(createdProduct, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating product {Name}", request.Name);
            throw;
        }
    }

    public async Task<ProductDto> UpdateProductAsync(string productId, UpdateProductRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Updating product: {ProductId}", productId);

        try
        {
            var updateRequest = new ErpProductRequest
            {
                Name = request.Name ?? string.Empty,
                Description = request.Description,
                Price = request.Price ?? 0,
                Category = request.Category ?? string.Empty,
                IsActive = request.IsActive ?? true
            };

            var erpResponse = await _erpService.UpdateProductAsync(productId, updateRequest, cancellationToken);
            if (!erpResponse.Success || erpResponse.Data == null)
            {
                throw new InvalidOperationException($"Failed to update product: {erpResponse.ErrorMessage}");
            }

            var updatedProduct = erpResponse.Data;
            _logger.LogDebug("Updated product {ProductId} in ERP", productId);

            // Note: Cache invalidation is now handled by MediatR CacheInvalidationBehaviour

            // Get current stock for the response
            var warehouseResponse = await _warehouseService.GetStockAsync(productId, cancellationToken);
            var stock = warehouseResponse.Success ? warehouseResponse.Data : null;

            return MapToProductDto(updatedProduct, stock);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating product {ProductId}", productId);
            throw;
        }
    }

    public async Task<bool> DeleteProductAsync(string productId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Deleting product: {ProductId}", productId);

        try
        {
            var erpResponse = await _erpService.DeleteProductAsync(productId, cancellationToken);
            if (!erpResponse.Success)
            {
                _logger.LogError("Failed to delete product {ProductId}: {ErrorMessage}", productId, erpResponse.ErrorMessage);
                return false;
            }

            _logger.LogDebug("Deleted product {ProductId} from ERP", productId);

            // Note: Cache invalidation is now handled by MediatR CacheInvalidationBehaviour
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting product {ProductId}", productId);
            return false;
        }
    }

    private static ProductDto MapToProductDto(ErpProduct erpProduct, WarehouseStock? stock)
    {
        return new ProductDto
        {
            Id = erpProduct.Id,
            Name = erpProduct.Name,
            Description = erpProduct.Description,
            Price = erpProduct.Price,
            Category = erpProduct.Category,
            IsActive = erpProduct.IsActive,
            StockQuantity = stock?.Quantity ?? 0,
            WarehouseLocation = stock?.Location,
            InStock = stock?.Quantity > 0
        };
    }

    private static ProductV2Dto MapToProductV2Dto(ErpProduct erpProduct, WarehouseStock? stock)
    {
        return new ProductV2Dto
        {
            Id = erpProduct.Id,
            Name = erpProduct.Name,
            Description = erpProduct.Description,
            Price = erpProduct.Price,
            Category = erpProduct.Category,
            IsActive = erpProduct.IsActive,
            StockQuantity = stock?.Quantity ?? 0,
            WarehouseLocation = stock?.Location,
            InStock = stock?.Quantity > 0,
            // V2 specific metadata
            Metadata = new Dictionary<string, object>
            {
                ["AvailabilityStatus"] = stock?.Quantity > 10 ? "High" : stock?.Quantity > 0 ? "Low" : "OutOfStock",
                ["EstimatedDeliveryDays"] = stock?.Quantity > 0 ? 2 : 7,
                ["LastStockUpdate"] = stock?.LastUpdated.ToString() ?? "Unknown"
            }
        };
    }
}