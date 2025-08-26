using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IntegrationGateway.Models.DTOs;
using IntegrationGateway.Services.Interfaces;

namespace IntegrationGateway.Controllers.V1;

[ApiController]
[Route("api/v1/[controller]")]
[ApiVersion("1.0")]
[Produces("application/json")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(IProductService productService, ILogger<ProductsController> logger)
    {
        _productService = productService;
        _logger = logger;
    }

    /// <summary>
    /// Get all products with pagination
    /// </summary>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="pageSize">Page size (default: 50, max: 100)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated list of products with stock information</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ProductListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ProductListResponse>> GetProducts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate parameters
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 50;

            _logger.LogInformation("Getting products list - Page: {Page}, Size: {PageSize}", page, pageSize);

            var response = await _productService.GetProductsAsync(page, pageSize, cancellationToken);
            
            _logger.LogInformation("Retrieved {Count} products", response.Products.Count);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting products list");
            
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
    /// Get a specific product by ID
    /// </summary>
    /// <param name="id">Product ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Product details with stock information</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ProductDto>> GetProduct(
        string id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting product: {ProductId}", id);

            var product = await _productService.GetProductAsync(id, cancellationToken);
            
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

            _logger.LogInformation("Retrieved product: {ProductId}", id);
            return Ok(product);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting product: {ProductId}", id);
            
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
    /// Create a new product
    /// </summary>
    /// <param name="request">Product creation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created product</returns>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ProductDto>> CreateProduct(
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

            _logger.LogInformation("Creating product: {ProductName}, IdempotencyKey: {IdempotencyKey}", 
                request.Name, idempotencyKey);

            var product = await _productService.CreateProductAsync(request, idempotencyKey, cancellationToken);
            
            _logger.LogInformation("Created product: {ProductId}", product.Id);
            return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation creating product: {ProductName}", request.Name);
            
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
            _logger.LogError(ex, "Error creating product: {ProductName}", request.Name);
            
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
    /// Update an existing product
    /// </summary>
    /// <param name="id">Product ID</param>
    /// <param name="request">Product update request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated product</returns>
    [HttpPut("{id}")]
    [Authorize]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ProductDto>> UpdateProduct(
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

            _logger.LogInformation("Updating product: {ProductId}, IdempotencyKey: {IdempotencyKey}", 
                id, idempotencyKey);

            var product = await _productService.UpdateProductAsync(id, request, idempotencyKey, cancellationToken);
            
            _logger.LogInformation("Updated product: {ProductId}", id);
            return Ok(product);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            _logger.LogWarning(ex, "Product not found for update: {ProductId}", id);
            
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
            _logger.LogWarning(ex, "Invalid operation updating product: {ProductId}", id);
            
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
            _logger.LogError(ex, "Error updating product: {ProductId}", id);
            
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

    /// <summary>
    /// Delete a product (soft delete)
    /// </summary>
    /// <param name="id">Product ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success status</returns>
    [HttpDelete("{id}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteProduct(
        string id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Deleting product: {ProductId}", id);

            var success = await _productService.DeleteProductAsync(id, cancellationToken);
            
            if (!success)
            {
                _logger.LogWarning("Product not found for deletion: {ProductId}", id);
                
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

            _logger.LogInformation("Deleted product: {ProductId}", id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting product: {ProductId}", id);
            
            var errorResponse = new ErrorResponse
            {
                Type = "internal_error",
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred while deleting the product",
                Status = StatusCodes.Status500InternalServerError,
                TraceId = HttpContext.TraceIdentifier
            };
            
            return StatusCode(StatusCodes.Status500InternalServerError, errorResponse);
        }
    }
}