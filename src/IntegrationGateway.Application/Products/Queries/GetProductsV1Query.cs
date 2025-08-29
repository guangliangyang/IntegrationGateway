using IntegrationGateway.Application.Common.Behaviours;
using IntegrationGateway.Models.DTOs;
using IntegrationGateway.Services.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IntegrationGateway.Application.Products.Queries;

/// <summary>
/// Query to get paginated list of products
/// </summary>
[Cacheable(5)] // Cache for 5 seconds
public record GetProductsV1Query(int Page = 1, int PageSize = 50) : IRequest<ProductListResponse>;

/// <summary>
/// Handler for GetProductsV1Query
/// </summary>
public class GetProductsQueryV1Handler : IRequestHandler<GetProductsV1Query, ProductListResponse>
{
    private readonly IProductService _productService;
    private readonly ILogger<GetProductsQueryV1Handler> _logger;

    public GetProductsQueryV1Handler(IProductService productService, ILogger<GetProductsQueryV1Handler> logger)
    {
        _productService = productService;
        _logger = logger;
    }

    public async Task<ProductListResponse> Handle(GetProductsV1Query request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting products - Page: {Page}, Size: {PageSize}", request.Page, request.PageSize);

        var result = await _productService.GetProductsAsync(request.Page, request.PageSize, cancellationToken);

        _logger.LogInformation("Retrieved {Count} products", result.Products.Count);

        return result;
    }
}