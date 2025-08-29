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
        ValidatePaginationParameters(page, pageSize);
        _logger.LogDebug("Getting products list, page: {Page}, size: {PageSize}", page, pageSize);

        var erpProducts = await GetErpProductsAsync(cancellationToken);
        var stockLookup = await GetBulkStockLookupAsync(erpProducts.Select(p => p.Id), cancellationToken);
        
        var mergedProducts = erpProducts.Select(erpProduct =>
        {
            stockLookup.TryGetValue(erpProduct.Id, out var stock);
            return MapToProductDto(erpProduct, stock);
        }).ToList();

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

    public async Task<ProductListV2Response> GetProductsV2Async(int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        ValidatePaginationParameters(page, pageSize);
        _logger.LogDebug("Getting products list V2, page: {Page}, size: {PageSize}", page, pageSize);

        var erpProducts = await GetErpProductsAsync(cancellationToken);
        var stockLookup = await GetBulkStockLookupAsync(erpProducts.Select(p => p.Id), cancellationToken);
        
        var mergedProducts = erpProducts.Select(erpProduct =>
        {
            stockLookup.TryGetValue(erpProduct.Id, out var stock);
            return MapToProductV2Dto(erpProduct, stock);
        }).ToList();

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

    public async Task<ProductDto?> GetProductAsync(string productId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(productId))
            throw new ArgumentException("Product ID cannot be null or empty", nameof(productId));
            
        _logger.LogDebug("Getting product by ID: {ProductId}", productId);

        var erpProduct = await GetErpProductByIdAsync(productId, cancellationToken);
        if (erpProduct == null)
        {
            return null;
        }

        var stock = await GetWarehouseStockAsync(productId, cancellationToken);
        var productDto = MapToProductDto(erpProduct, stock);
        
        _logger.LogDebug("Returning product {ProductId}", productId);
        return productDto;
    }

    public async Task<ProductV2Dto?> GetProductV2Async(string productId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(productId))
            throw new ArgumentException("Product ID cannot be null or empty", nameof(productId));
            
        _logger.LogDebug("Getting product by ID V2: {ProductId}", productId);

        var erpProduct = await GetErpProductByIdAsync(productId, cancellationToken);
        if (erpProduct == null)
        {
            return null;
        }

        var stock = await GetWarehouseStockAsync(productId, cancellationToken);
        var productV2Dto = MapToProductV2Dto(erpProduct, stock);
        
        _logger.LogDebug("Returning product V2 {ProductId}", productId);
        return productV2Dto;
    }

    public async Task<ProductDto> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));
            
        _logger.LogDebug("Creating product: {Name}", request.Name);

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
        
        return MapToProductDto(createdProduct, null);
    }

    public async Task<ProductDto> UpdateProductAsync(string productId, UpdateProductRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(productId))
            throw new ArgumentException("Product ID cannot be null or empty", nameof(productId));
        if (request == null)
            throw new ArgumentNullException(nameof(request));
            
        _logger.LogDebug("Updating product: {ProductId}", productId);

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

        var stock = await GetWarehouseStockAsync(productId, cancellationToken);
        return MapToProductDto(updatedProduct, stock);
    }

    public async Task<bool> DeleteProductAsync(string productId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(productId))
            throw new ArgumentException("Product ID cannot be null or empty", nameof(productId));
            
        _logger.LogDebug("Deleting product: {ProductId}", productId);

        var erpResponse = await _erpService.DeleteProductAsync(productId, cancellationToken);
        if (!erpResponse.Success)
        {
            _logger.LogError("Failed to delete product {ProductId}: {ErrorMessage}", productId, erpResponse.ErrorMessage);
            return false;
        }

        _logger.LogDebug("Deleted product {ProductId} from ERP", productId);
        return true;
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

    private static void ValidatePaginationParameters(int page, int pageSize)
    {
        if (page <= 0)
            throw new ArgumentException("Page number must be greater than 0", nameof(page));
        if (pageSize <= 0 || pageSize > 1000)
            throw new ArgumentException("Page size must be between 1 and 1000", nameof(pageSize));
    }

    private async Task<List<ErpProduct>> GetErpProductsAsync(CancellationToken cancellationToken)
    {
        var erpResponse = await _erpService.GetProductsAsync(cancellationToken);
        if (!erpResponse.Success || erpResponse.Data == null)
        {
            _logger.LogError("Failed to get products from ERP: {ErrorMessage}", erpResponse.ErrorMessage);
            throw new InvalidOperationException($"ERP service unavailable: {erpResponse.ErrorMessage}");
        }

        _logger.LogDebug("Retrieved {Count} products from ERP", erpResponse.Data.Count);
        return erpResponse.Data;
    }

    private async Task<ErpProduct?> GetErpProductByIdAsync(string productId, CancellationToken cancellationToken)
    {
        var erpResponse = await _erpService.GetProductAsync(productId, cancellationToken);
        if (!erpResponse.Success || erpResponse.Data == null)
        {
            _logger.LogWarning("Product {ProductId} not found in ERP: {ErrorMessage}", productId, erpResponse.ErrorMessage);
            return null;
        }

        _logger.LogDebug("Retrieved product {ProductId} from ERP", productId);
        return erpResponse.Data;
    }

    private async Task<Dictionary<string, WarehouseStock>> GetBulkStockLookupAsync(IEnumerable<string> productIds, CancellationToken cancellationToken)
    {
        var productIdList = productIds.ToList();
        var warehouseResponse = await _warehouseService.GetBulkStockAsync(productIdList, cancellationToken);
        
        if (warehouseResponse.Success && warehouseResponse.Data != null)
        {
            var stockLookup = warehouseResponse.Data.Stocks.ToDictionary(s => s.ProductId, s => s);
            _logger.LogDebug("Retrieved stock for {Count} products from Warehouse", stockLookup.Count);
            return stockLookup;
        }
        
        _logger.LogWarning("Failed to get stock from Warehouse: {ErrorMessage}. Continuing without stock data.", warehouseResponse.ErrorMessage);
        return new Dictionary<string, WarehouseStock>();
    }

    private async Task<WarehouseStock?> GetWarehouseStockAsync(string productId, CancellationToken cancellationToken)
    {
        var warehouseResponse = await _warehouseService.GetStockAsync(productId, cancellationToken);
        
        if (warehouseResponse.Success && warehouseResponse.Data != null)
        {
            _logger.LogDebug("Retrieved stock for product {ProductId} from Warehouse", productId);
            return warehouseResponse.Data;
        }

        _logger.LogWarning("Failed to get stock for product {ProductId}: {ErrorMessage}. Continuing without stock data.", 
            productId, warehouseResponse.ErrorMessage);
        return null;
    }
}