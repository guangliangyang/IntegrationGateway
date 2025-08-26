using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using IntegrationGateway.Models;
using IntegrationGateway.Models.DTOs;
using IntegrationGateway.Models.External;
using IntegrationGateway.Models.Common;
using IntegrationGateway.Services.Configuration;
using IntegrationGateway.Services.Interfaces;

namespace IntegrationGateway.Services.Implementation;

public class ProductService : IProductService
{
    private readonly IErpService _erpService;
    private readonly IWarehouseService _warehouseService;
    private readonly ICacheService _cacheService;
    private readonly IIdempotencyService _idempotencyService;
    private readonly ILogger<ProductService> _logger;
    private readonly CacheOptions _cacheOptions;

    public ProductService(
        IErpService erpService,
        IWarehouseService warehouseService,
        ICacheService cacheService,
        IIdempotencyService idempotencyService,
        ILogger<ProductService> logger,
        IOptions<CacheOptions> cacheOptions)
    {
        _erpService = erpService;
        _warehouseService = warehouseService;
        _cacheService = cacheService;
        _idempotencyService = idempotencyService;
        _logger = logger;
        _cacheOptions = cacheOptions.Value;
    }

    public async Task<ProductListResponse> GetProductsAsync(int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"products:list:page:{page}:size:{pageSize}";
        
        _logger.LogDebug("Getting products list, page: {Page}, size: {PageSize}", page, pageSize);

        // Try cache first
        var cachedResponse = await _cacheService.GetAsync<ProductListResponse>(cacheKey, cancellationToken);
        if (cachedResponse != null)
        {
            _logger.LogDebug("Products list found in cache");
            return cachedResponse;
        }

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

            // Cache the response
            await _cacheService.SetAsync(
                cacheKey, 
                response, 
                TimeSpan.FromMinutes(_cacheOptions.ProductListExpirationMinutes), 
                cancellationToken);

            _logger.LogDebug("Successfully retrieved and cached {Count} products", paginatedProducts.Count);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting products list");
            return new ProductListResponse { Products = new List<ProductDto>() };
        }
    }

    public async Task<ProductListV2Response> GetProductsV2Async(int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"products:list:v2:page:{page}:size:{pageSize}";
        
        _logger.LogDebug("Getting products list v2, page: {Page}, size: {PageSize}", page, pageSize);

        // Try cache first
        var cachedResponse = await _cacheService.GetAsync<ProductListV2Response>(cacheKey, cancellationToken);
        if (cachedResponse != null)
        {
            _logger.LogDebug("Products list v2 found in cache");
            return cachedResponse;
        }

        try
        {
            // Get base response from v1
            var v1Response = await GetProductsAsync(page, pageSize, cancellationToken);
            
            // Convert to v2 with enhanced fields
            var v2Products = v1Response.Products.Select(p => new ProductV2Dto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Price = p.Price,
                Category = p.Category,
                IsActive = p.IsActive,
                StockQuantity = p.StockQuantity,
                InStock = p.InStock,
                WarehouseLocation = p.WarehouseLocation,
                Supplier = GenerateSupplierInfo(p.Category),
                Tags = GenerateTags(p.Category, p.Name),
                Metadata = new Dictionary<string, object>
                {
                    { "lastUpdated", DateTime.UtcNow },
                    { "version", "2.0" },
                    { "priceHistory", new { current = p.Price, trend = "stable" } }
                }
            }).ToList();

            var response = new ProductListV2Response
            {
                Products = v2Products,
                Total = v1Response.Total,
                Page = v1Response.Page,
                PageSize = v1Response.PageSize,
                Metadata = new Dictionary<string, object>
                {
                    { "apiVersion", "2.0" },
                    { "generatedAt", DateTime.UtcNow },
                    { "enhancedFields", new[] { "supplier", "tags", "metadata" } }
                }
            };

            // Cache the v2 response
            await _cacheService.SetAsync(
                cacheKey, 
                response, 
                TimeSpan.FromMinutes(_cacheOptions.ProductListExpirationMinutes), 
                cancellationToken);

            _logger.LogDebug("Successfully retrieved and cached {Count} products v2", v2Products.Count);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting products list v2");
            return new ProductListV2Response { Products = new List<ProductV2Dto>() };
        }
    }

    public async Task<ProductDto?> GetProductAsync(string productId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"product:detail:{productId}";
        
        _logger.LogDebug("Getting product: {ProductId}", productId);

        // Try cache first
        var cachedProduct = await _cacheService.GetAsync<ProductDto>(cacheKey, cancellationToken);
        if (cachedProduct != null)
        {
            _logger.LogDebug("Product found in cache: {ProductId}", productId);
            return cachedProduct;
        }

        try
        {
            // Get product from ERP and stock from Warehouse in parallel
            var erpTask = _erpService.GetProductAsync(productId, cancellationToken);
            var stockTask = _warehouseService.GetStockAsync(productId, cancellationToken);

            await Task.WhenAll(erpTask, stockTask);

            var erpResponse = await erpTask;
            var stockResponse = await stockTask;

            if (!erpResponse.Success || erpResponse.Data == null)
            {
                _logger.LogWarning("Product not found in ERP: {ProductId}", productId);
                return null;
            }

            var stock = stockResponse.Success ? stockResponse.Data : null;
            var product = MapToProductDto(erpResponse.Data, stock);

            // Cache the product
            await _cacheService.SetAsync(
                cacheKey, 
                product, 
                TimeSpan.FromMinutes(_cacheOptions.ProductDetailExpirationMinutes), 
                cancellationToken);

            _logger.LogDebug("Successfully retrieved and cached product: {ProductId}", productId);
            return product;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting product: {ProductId}", productId);
            return null;
        }
    }

    public async Task<ProductV2Dto?> GetProductV2Async(string productId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"product:detail:v2:{productId}";
        
        _logger.LogDebug("Getting product v2: {ProductId}", productId);

        // Try cache first
        var cachedProduct = await _cacheService.GetAsync<ProductV2Dto>(cacheKey, cancellationToken);
        if (cachedProduct != null)
        {
            _logger.LogDebug("Product v2 found in cache: {ProductId}", productId);
            return cachedProduct;
        }

        try
        {
            // Get base product from v1
            var v1Product = await GetProductAsync(productId, cancellationToken);
            if (v1Product == null)
            {
                return null;
            }

            // Convert to v2 with enhanced fields
            var v2Product = new ProductV2Dto
            {
                Id = v1Product.Id,
                Name = v1Product.Name,
                Description = v1Product.Description,
                Price = v1Product.Price,
                Category = v1Product.Category,
                IsActive = v1Product.IsActive,
                StockQuantity = v1Product.StockQuantity,
                InStock = v1Product.InStock,
                WarehouseLocation = v1Product.WarehouseLocation,
                Supplier = GenerateSupplierInfo(v1Product.Category),
                Tags = GenerateTags(v1Product.Category, v1Product.Name),
                Metadata = new Dictionary<string, object>
                {
                    { "lastUpdated", DateTime.UtcNow },
                    { "version", "2.0" },
                    { "priceHistory", new { current = v1Product.Price, trend = "stable" } },
                    { "stockLevel", v1Product.StockQuantity > 10 ? "high" : v1Product.StockQuantity > 0 ? "low" : "out" }
                }
            };

            // Cache the v2 product
            await _cacheService.SetAsync(
                cacheKey, 
                v2Product, 
                TimeSpan.FromMinutes(_cacheOptions.ProductDetailExpirationMinutes), 
                cancellationToken);

            _logger.LogDebug("Successfully retrieved and cached product v2: {ProductId}", productId);
            return v2Product;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting product v2: {ProductId}", productId);
            return null;
        }
    }

    public async Task<ProductDto> CreateProductAsync(CreateProductRequest request, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var bodyHash = IdempotencyKey.GenerateBodyHash(System.Text.Json.JsonSerializer.Serialize(request));
        var operation = "CREATE_PRODUCT";

        _logger.LogDebug("Creating product: {ProductName}, IdempotencyKey: {IdempotencyKey}", request.Name, idempotencyKey);

        // Check idempotency
        var existingOperation = await _idempotencyService.GetAsync(idempotencyKey, operation, bodyHash, cancellationToken);
        if (existingOperation != null && !string.IsNullOrEmpty(existingOperation.ResponseBody))
        {
            _logger.LogDebug("Idempotent operation found for key: {IdempotencyKey}", idempotencyKey);
            var existingProduct = System.Text.Json.JsonSerializer.Deserialize<ProductDto>(existingOperation.ResponseBody);
            return existingProduct!;
        }

        try
        {
            // Create product in ERP
            var erpRequest = new ErpProductRequest
            {
                Name = request.Name,
                Description = request.Description,
                Price = request.Price,
                Category = request.Category,
                IsActive = request.IsActive
            };

            var erpResponse = await _erpService.CreateProductAsync(erpRequest, cancellationToken);
            
            if (!erpResponse.Success || erpResponse.Data == null)
            {
                var errorMessage = $"Failed to create product in ERP: {erpResponse.ErrorMessage}";
                _logger.LogError(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }

            // Get initial stock (likely 0 for new products)
            var stockResponse = await _warehouseService.GetStockAsync(erpResponse.Data.Id, cancellationToken);
            var stock = stockResponse.Success ? stockResponse.Data : null;

            var product = MapToProductDto(erpResponse.Data, stock);

            // Store idempotency result
            var idempotencyRecord = new IdempotencyKey
            {
                Key = idempotencyKey,
                Operation = operation,
                BodyHash = bodyHash,
                ResponseBody = System.Text.Json.JsonSerializer.Serialize(product),
                ResponseStatusCode = 201
            };
            await _idempotencyService.SetAsync(idempotencyRecord, cancellationToken);

            // Invalidate cache
            await _cacheService.RemoveByPatternAsync("products:list", cancellationToken);

            _logger.LogDebug("Successfully created product: {ProductId}", product.Id);
            return product;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating product: {ProductName}", request.Name);
            throw;
        }
    }

    public async Task<ProductDto> UpdateProductAsync(string productId, UpdateProductRequest request, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var bodyHash = IdempotencyKey.GenerateBodyHash(System.Text.Json.JsonSerializer.Serialize(request));
        var operation = $"UPDATE_PRODUCT_{productId}";

        _logger.LogDebug("Updating product: {ProductId}, IdempotencyKey: {IdempotencyKey}", productId, idempotencyKey);

        // Check idempotency
        var existingOperation = await _idempotencyService.GetAsync(idempotencyKey, operation, bodyHash, cancellationToken);
        if (existingOperation != null && !string.IsNullOrEmpty(existingOperation.ResponseBody))
        {
            _logger.LogDebug("Idempotent operation found for key: {IdempotencyKey}", idempotencyKey);
            var existingProduct = System.Text.Json.JsonSerializer.Deserialize<ProductDto>(existingOperation.ResponseBody);
            return existingProduct!;
        }

        try
        {
            // Build ERP update request with only provided fields
            var erpRequest = new ErpProductRequest();
            
            // Get existing product first to fill in unchanged fields
            var existingErpResponse = await _erpService.GetProductAsync(productId, cancellationToken);
            if (!existingErpResponse.Success || existingErpResponse.Data == null)
            {
                var errorMessage = $"Product not found: {productId}";
                _logger.LogWarning(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }

            var existingProduct = existingErpResponse.Data;
            
            // Update only provided fields
            erpRequest.Name = request.Name ?? existingProduct.Name;
            erpRequest.Description = request.Description ?? existingProduct.Description;
            erpRequest.Price = request.Price ?? existingProduct.Price;
            erpRequest.Category = request.Category ?? existingProduct.Category;
            erpRequest.IsActive = request.IsActive ?? existingProduct.IsActive;
            erpRequest.Supplier = existingProduct.Supplier;

            var erpResponse = await _erpService.UpdateProductAsync(productId, erpRequest, cancellationToken);
            
            if (!erpResponse.Success || erpResponse.Data == null)
            {
                var errorMessage = $"Failed to update product in ERP: {erpResponse.ErrorMessage}";
                _logger.LogError(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }

            // Get updated stock
            var stockResponse = await _warehouseService.GetStockAsync(productId, cancellationToken);
            var stock = stockResponse.Success ? stockResponse.Data : null;

            var product = MapToProductDto(erpResponse.Data, stock);

            // Store idempotency result
            var idempotencyRecord = new IdempotencyKey
            {
                Key = idempotencyKey,
                Operation = operation,
                BodyHash = bodyHash,
                ResponseBody = System.Text.Json.JsonSerializer.Serialize(product),
                ResponseStatusCode = 200
            };
            await _idempotencyService.SetAsync(idempotencyRecord, cancellationToken);

            // Invalidate cache
            await _cacheService.RemoveAsync($"product:detail:{productId}", cancellationToken);
            await _cacheService.RemoveAsync($"product:detail:v2:{productId}", cancellationToken);
            await _cacheService.RemoveByPatternAsync("products:list", cancellationToken);

            _logger.LogDebug("Successfully updated product: {ProductId}", productId);
            return product;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error updating product: {ProductId}", productId);
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
                _logger.LogWarning("Failed to delete product from ERP: {ProductId}, Error: {ErrorMessage}", 
                    productId, erpResponse.ErrorMessage);
                return false;
            }

            // Invalidate cache
            await _cacheService.RemoveAsync($"product:detail:{productId}", cancellationToken);
            await _cacheService.RemoveAsync($"product:detail:v2:{productId}", cancellationToken);
            await _cacheService.RemoveByPatternAsync("products:list", cancellationToken);

            _logger.LogDebug("Successfully deleted product: {ProductId}", productId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting product: {ProductId}", productId);
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
            StockQuantity = stock?.AvailableQuantity ?? 0,
            InStock = stock?.InStock ?? false,
            WarehouseLocation = stock?.Location
        };
    }

    private static string GenerateSupplierInfo(string category)
    {
        // Simple supplier mapping based on category
        return category.ToLowerInvariant() switch
        {
            "electronics" => "TechSupplier Inc",
            "clothing" => "Fashion Wholesale Ltd",
            "books" => "BookDistributor Co",
            "food" => "Fresh Foods Supply",
            _ => "General Supplier"
        };
    }

    private static List<string> GenerateTags(string category, string name)
    {
        var tags = new List<string> { category.ToLowerInvariant() };
        
        // Add some intelligent tags based on name
        if (name.ToLowerInvariant().Contains("premium"))
            tags.Add("premium");
        if (name.ToLowerInvariant().Contains("eco") || name.ToLowerInvariant().Contains("green"))
            tags.Add("eco-friendly");
        if (name.ToLowerInvariant().Contains("sale") || name.ToLowerInvariant().Contains("discount"))
            tags.Add("on-sale");
        
        return tags;
    }
}