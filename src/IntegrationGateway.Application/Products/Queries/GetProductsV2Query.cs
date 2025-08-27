using IntegrationGateway.Application.Common.Behaviours;
using IntegrationGateway.Models.DTOs;
using IntegrationGateway.Services.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IntegrationGateway.Application.Products.Queries;

/// <summary>
/// Query to get paginated list of products with V2 enhanced information
/// </summary>
[Cacheable(300)] // Cache for 5 minutes
public record GetProductsV2Query(int Page = 1, int PageSize = 50) : IRequest<ProductListV2Response>;

/// <summary>
/// Handler for GetProductsV2Query
/// </summary>
public class GetProductsV2QueryHandler : IRequestHandler<GetProductsV2Query, ProductListV2Response>
{
    private readonly IProductService _productService;
    private readonly ILogger<GetProductsV2QueryHandler> _logger;

    public GetProductsV2QueryHandler(IProductService productService, ILogger<GetProductsV2QueryHandler> logger)
    {
        _productService = productService;
        _logger = logger;
    }

    public async Task<ProductListV2Response> Handle(GetProductsV2Query request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting products V2 - Page: {Page}, Size: {PageSize}", request.Page, request.PageSize);

        var result = await _productService.GetProductsV2Async(request.Page, request.PageSize, cancellationToken);

        _logger.LogInformation("Retrieved {Count} products V2", result.Products.Count);

        return result;
    }
}

/// <summary>
/// Query to get a single product by ID with V2 enhanced information
/// </summary>
[Cacheable(600)] // Cache for 10 minutes
public record GetProductV2Query(string Id) : IRequest<ProductV2Dto?>;

/// <summary>
/// Handler for GetProductV2Query
/// </summary>
public class GetProductV2QueryHandler : IRequestHandler<GetProductV2Query, ProductV2Dto?>
{
    private readonly IProductService _productService;
    private readonly ILogger<GetProductV2QueryHandler> _logger;

    public GetProductV2QueryHandler(IProductService productService, ILogger<GetProductV2QueryHandler> logger)
    {
        _productService = productService;
        _logger = logger;
    }

    public async Task<ProductV2Dto?> Handle(GetProductV2Query request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting product V2: {ProductId}", request.Id);

        var product = await _productService.GetProductV2Async(request.Id, cancellationToken);

        if (product == null)
        {
            _logger.LogWarning("Product V2 not found: {ProductId}", request.Id);
            return null;
        }

        _logger.LogInformation("Retrieved product V2: {ProductId}", request.Id);
        return product;
    }
}