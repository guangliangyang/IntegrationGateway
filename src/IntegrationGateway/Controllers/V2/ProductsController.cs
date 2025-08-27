using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IntegrationGateway.Models.DTOs;
using IntegrationGateway.Services.Interfaces;

namespace IntegrationGateway.Controllers.V2;

/// <summary>
/// V2 Products Controller - Enhanced version with additional fields and batch operations
/// Inherits from V1 and overrides methods to return V2 format responses
/// </summary>
[ApiController]
[Route("api/v2/[controller]")]
[ApiVersion("2.0")]
[Produces("application/json")]
public class ProductsController : V1.ProductsController
{
    public ProductsController(IProductService productService, ILogger<ProductsController> logger)
        : base(productService, logger)
    {
    }

    /// <summary>
    /// Get all products with enhanced information and pagination (V2)
    /// </summary>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="pageSize">Page size (default: 50, max: 100)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated list of products with enhanced fields</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ProductListV2Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public override async Task<ActionResult<ProductListResponse>> GetProducts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate parameters (same as V1)
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 50;

            _logger.LogInformation("Getting products list V2 - Page: {Page}, Size: {PageSize}", page, pageSize);

            // Use V2 service method for enhanced response
            var response = await _productService.GetProductsV2Async(page, pageSize, cancellationToken);
            
            _logger.LogInformation("Retrieved {Count} products V2", response.Products.Count);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting products list V2");
            
            var errorResponse = new ErrorResponse
            {
                Type = "internal_error",
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred while retrieving products",
                Status = StatusCodes.Status500InternalServerError,
                TraceId = HttpContext.TraceIdentifier
            };
            
            return StatusCode(StatusCodes.Status500InternalServerError, errorResponse);
        }
    }

    /// <summary>
    /// Get a specific product by ID with enhanced information (V2)
    /// </summary>
    /// <param name="id">Product ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Product details with enhanced fields</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ProductV2Dto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public override async Task<ActionResult<ProductDto>> GetProduct(
        string id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting product V2: {ProductId}", id);

            // Use V2 service method for enhanced response
            var product = await _productService.GetProductV2Async(id, cancellationToken);
            
            if (product == null)
            {
                _logger.LogWarning("Product not found: {ProductId}", id);
                
                var notFoundResponse = new ErrorResponse
                {
                    Type = "not_found",
                    Title = "Product Not Found",
                    Detail = $"Product with ID '{id}' was not found",
                    Status = StatusCodes.Status404NotFound,
                    TraceId = HttpContext.TraceIdentifier
                };
                
                return NotFound(notFoundResponse);
            }

            _logger.LogInformation("Retrieved product V2: {ProductId}", id);
            return Ok(product);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting product V2: {ProductId}", id);
            
            var errorResponse = new ErrorResponse
            {
                Type = "internal_error",
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred while retrieving the product",
                Status = StatusCodes.Status500InternalServerError,
                TraceId = HttpContext.TraceIdentifier
            };
            
            return StatusCode(StatusCodes.Status500InternalServerError, errorResponse);
        }
    }

    /// <summary>
    /// Create a new product (V2 - same functionality as V1, returns enhanced response)
    /// </summary>
    /// <param name="request">Product creation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created product with enhanced information</returns>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(ProductV2Dto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public override async Task<ActionResult<ProductDto>> CreateProduct(
        [FromBody] CreateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var idempotencyKey = HttpContext.Items["IdempotencyKey"] as string;
            if (string.IsNullOrEmpty(idempotencyKey))
            {
                var errorResponse = new ErrorResponse
                {
                    Type = "missing_idempotency_key",
                    Title = "Missing Idempotency Key",
                    Detail = "Idempotency-Key header is required for this operation",
                    Status = StatusCodes.Status400BadRequest,
                    TraceId = HttpContext.TraceIdentifier
                };
                
                return BadRequest(errorResponse);
            }

            _logger.LogInformation("Creating product V2: {ProductName}, IdempotencyKey: {IdempotencyKey}", 
                request.Name, idempotencyKey);

            // Create using V1 service, then get V2 representation
            var v1Product = await _productService.CreateProductAsync(request, idempotencyKey, cancellationToken);
            var v2Product = await _productService.GetProductV2Async(v1Product.Id, cancellationToken);
            
            _logger.LogInformation("Created product V2: {ProductId}", v1Product.Id);
            return CreatedAtAction(nameof(GetProduct), new { id = v1Product.Id }, v2Product);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation creating product V2: {ProductName}", request.Name);
            
            var errorResponse = new ErrorResponse
            {
                Type = "invalid_operation",
                Title = "Invalid Operation",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.TraceIdentifier
            };
            
            return BadRequest(errorResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating product V2: {ProductName}", request.Name);
            
            var errorResponse = new ErrorResponse
            {
                Type = "internal_error",
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred while creating the product",
                Status = StatusCodes.Status500InternalServerError,
                TraceId = HttpContext.TraceIdentifier
            };
            
            return StatusCode(StatusCodes.Status500InternalServerError, errorResponse);
        }
    }

    /// <summary>
    /// Update an existing product (V2 - same functionality as V1, returns enhanced response)
    /// </summary>
    /// <param name="id">Product ID</param>
    /// <param name="request">Product update request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated product with enhanced information</returns>
    [HttpPut("{id}")]
    [Authorize]
    [ProducesResponseType(typeof(ProductV2Dto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public override async Task<ActionResult<ProductDto>> UpdateProduct(
        string id,
        [FromBody] UpdateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var idempotencyKey = HttpContext.Items["IdempotencyKey"] as string;
            if (string.IsNullOrEmpty(idempotencyKey))
            {
                var errorResponse = new ErrorResponse
                {
                    Type = "missing_idempotency_key",
                    Title = "Missing Idempotency Key",
                    Detail = "Idempotency-Key header is required for this operation",
                    Status = StatusCodes.Status400BadRequest,
                    TraceId = HttpContext.TraceIdentifier
                };
                
                return BadRequest(errorResponse);
            }

            _logger.LogInformation("Updating product V2: {ProductId}, IdempotencyKey: {IdempotencyKey}", 
                id, idempotencyKey);

            // Update using V1 service, then get V2 representation
            var v1Product = await _productService.UpdateProductAsync(id, request, idempotencyKey, cancellationToken);
            var v2Product = await _productService.GetProductV2Async(id, cancellationToken);
            
            _logger.LogInformation("Updated product V2: {ProductId}", id);
            return Ok(v2Product);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            _logger.LogWarning(ex, "Product not found for update V2: {ProductId}", id);
            
            var notFoundResponse = new ErrorResponse
            {
                Type = "not_found",
                Title = "Product Not Found",
                Detail = ex.Message,
                Status = StatusCodes.Status404NotFound,
                TraceId = HttpContext.TraceIdentifier
            };
            
            return NotFound(notFoundResponse);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation updating product V2: {ProductId}", id);
            
            var errorResponse = new ErrorResponse
            {
                Type = "invalid_operation",
                Title = "Invalid Operation",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.TraceIdentifier
            };
            
            return BadRequest(errorResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating product V2: {ProductId}", id);
            
            var errorResponse = new ErrorResponse
            {
                Type = "internal_error",
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred while updating the product",
                Status = StatusCodes.Status500InternalServerError,
                TraceId = HttpContext.TraceIdentifier
            };
            
            return StatusCode(StatusCodes.Status500InternalServerError, errorResponse);
        }
    }

    // DELETE method can be inherited from V1 since it has the same behavior
    // No need to override unless we need V2-specific delete logic

    // V2 New Features - Batch Operations Example

    /// <summary>
    /// Create multiple products in batch (V2 only feature)
    /// </summary>
    /// <param name="requests">List of product creation requests</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of created products with enhanced information</returns>
    [HttpPost("batch")]
    [Authorize]
    [ProducesResponseType(typeof(List<ProductV2Dto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<ProductV2Dto>>> CreateProductsBatch(
        [FromBody] List<CreateProductRequest> requests,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var idempotencyKey = HttpContext.Items["IdempotencyKey"] as string;
            if (string.IsNullOrEmpty(idempotencyKey))
            {
                return BadRequest(new ErrorResponse
                {
                    Type = "missing_idempotency_key",
                    Title = "Missing Idempotency Key",
                    Detail = "Idempotency-Key header is required for batch operations",
                    Status = StatusCodes.Status400BadRequest,
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            if (requests == null || !requests.Any())
            {
                return BadRequest(new ErrorResponse
                {
                    Type = "invalid_request",
                    Title = "Invalid Request",
                    Detail = "Request list cannot be empty",
                    Status = StatusCodes.Status400BadRequest,
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            _logger.LogInformation("Creating {Count} products in batch, IdempotencyKey: {IdempotencyKey}", 
                requests.Count, idempotencyKey);

            var results = new List<ProductV2Dto>();
            for (int i = 0; i < requests.Count; i++)
            {
                var batchKey = $"{idempotencyKey}-batch-{i}";
                var v1Product = await _productService.CreateProductAsync(requests[i], batchKey, cancellationToken);
                var v2Product = await _productService.GetProductV2Async(v1Product.Id, cancellationToken);
                if (v2Product != null)
                {
                    results.Add(v2Product);
                }
            }
            
            _logger.LogInformation("Created {Count} products in batch", results.Count);
            return Created("batch", results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating products batch");
            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
            {
                Type = "internal_error",
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred while creating products in batch",
                Status = StatusCodes.Status500InternalServerError,
                TraceId = HttpContext.TraceIdentifier
            });
        }
    }

    /// <summary>
    /// Get product history (V2 only feature)
    /// </summary>
    /// <param name="id">Product ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Product history information</returns>
    [HttpGet("{id}/history")]
    [ProducesResponseType(typeof(ProductHistoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ProductHistoryDto>> GetProductHistory(
        string id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting product history V2: {ProductId}", id);

            // For now, return a simple history (this would typically come from a service)
            var history = new ProductHistoryDto
            {
                ProductId = id,
                CreatedAt = DateTime.UtcNow.AddDays(-30),
                LastModified = DateTime.UtcNow,
                Versions = new List<ProductVersionDto>
                {
                    new() { Version = 1, ModifiedAt = DateTime.UtcNow.AddDays(-30), ModifiedBy = "System" },
                    new() { Version = 2, ModifiedAt = DateTime.UtcNow, ModifiedBy = "API User" }
                }
            };
            
            _logger.LogInformation("Retrieved product history V2: {ProductId}", id);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting product history V2: {ProductId}", id);
            
            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
            {
                Type = "internal_error",
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred while retrieving product history",
                Status = StatusCodes.Status500InternalServerError,
                TraceId = HttpContext.TraceIdentifier
            });
        }
    }
}