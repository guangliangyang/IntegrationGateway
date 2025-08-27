using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IntegrationGateway.Models.DTOs;
using IntegrationGateway.Application.Products.Commands;
using IntegrationGateway.Application.Products.Queries;
using MediatR;

namespace IntegrationGateway.Api.Controllers.V2;

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
    public ProductsController(IMediator mediator)
        : base(mediator)
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
        var query = new GetProductsV2Query(page, pageSize);
        var response = await _mediator.Send(query, cancellationToken);
        return Ok(response);
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
        var query = new GetProductV2Query(id);
        var product = await _mediator.Send(query, cancellationToken);
        
        if (product == null)
        {
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

        return Ok(product);
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

        var command = new CreateProductCommand
        {
            IdempotencyKey = idempotencyKey,
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            Category = request.Category,
            IsActive = request.IsActive
        };
        
        var v1Product = await _mediator.Send(command, cancellationToken);
        var v2Query = new GetProductV2Query(v1Product.Id);
        var v2Product = await _mediator.Send(v2Query, cancellationToken);
        
        return CreatedAtAction(nameof(GetProduct), new { id = v1Product.Id }, v2Product);
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

        var command = new UpdateProductCommand
        {
            Id = id,
            IdempotencyKey = idempotencyKey,
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            Category = request.Category,
            IsActive = request.IsActive
        };
        
        var v1Product = await _mediator.Send(command, cancellationToken);
        var v2Query = new GetProductV2Query(id);
        var v2Product = await _mediator.Send(v2Query, cancellationToken);
        
        return Ok(v2Product);
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

        var results = new List<ProductV2Dto>();
        for (int i = 0; i < requests.Count; i++)
        {
            var batchKey = $"{idempotencyKey}-batch-{i}";
            
            var command = new CreateProductCommand
            {
                IdempotencyKey = batchKey,
                Name = requests[i].Name,
                Description = requests[i].Description,
                Price = requests[i].Price,
                Category = requests[i].Category,
                IsActive = requests[i].IsActive
            };
            
            var v1Product = await _mediator.Send(command, cancellationToken);
            var v2Query = new GetProductV2Query(v1Product.Id);
            var v2Product = await _mediator.Send(v2Query, cancellationToken);
            
            if (v2Product != null)
            {
                results.Add(v2Product);
            }
        }
        
        return Created("batch", results);
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
        // For now, return a simple history (this would typically use MediatR query)
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
        
        return Ok(history);
    }
}