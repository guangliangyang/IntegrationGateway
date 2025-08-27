using IntegrationGateway.Application.Common.Behaviours;
using IntegrationGateway.Models.DTOs;
using IntegrationGateway.Services.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IntegrationGateway.Application.Products.Queries;

/// <summary>
/// Query to get paginated list of products
/// </summary>
[Cacheable(300)] // Cache for 5 minutes
public record GetProductsQuery(int Page = 1, int PageSize = 50) : IRequest<ProductListResponse>;

/// <summary>
/// Handler for GetProductsQuery
/// </summary>
public class GetProductsQueryHandler : IRequestHandler<GetProductsQuery, ProductListResponse>
{
    private readonly IProductService _productService;
    private readonly ILogger<GetProductsQueryHandler> _logger;

    public GetProductsQueryHandler(IProductService productService, ILogger<GetProductsQueryHandler> logger)
    {
        _productService = productService;
        _logger = logger;
    }

    public async Task<ProductListResponse> Handle(GetProductsQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting products - Page: {Page}, Size: {PageSize}", request.Page, request.PageSize);

        var result = await _productService.GetProductsAsync(request.Page, request.PageSize, cancellationToken);

        _logger.LogInformation("Retrieved {Count} products", result.Products.Count);

        return result;
    }
}