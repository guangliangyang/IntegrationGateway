using FluentValidation;
using IntegrationGateway.Application.Common.Behaviours;
using IntegrationGateway.Models.DTOs;
using IntegrationGateway.Services.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IntegrationGateway.Application.Products.Queries;

/// <summary>
/// Query to get a single product by ID
/// </summary>
[Cacheable(600)] // Cache for 10 minutes
public record GetProductQuery(string Id) : IRequest<ProductDto?>;

/// <summary>
/// Validator for GetProductQuery
/// </summary>
public class GetProductQueryValidator : AbstractValidator<GetProductQuery>
{
    public GetProductQueryValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Product ID is required")
            .MaximumLength(50)
            .WithMessage("Product ID must not exceed 50 characters");
    }
}

/// <summary>
/// Handler for GetProductQuery
/// </summary>
public class GetProductQueryHandler : IRequestHandler<GetProductQuery, ProductDto?>
{
    private readonly IProductService _productService;
    private readonly ILogger<GetProductQueryHandler> _logger;

    public GetProductQueryHandler(IProductService productService, ILogger<GetProductQueryHandler> logger)
    {
        _productService = productService;
        _logger = logger;
    }

    public async Task<ProductDto?> Handle(GetProductQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting product: {ProductId}", request.Id);

        var product = await _productService.GetProductAsync(request.Id, cancellationToken);

        if (product == null)
        {
            _logger.LogWarning("Product not found: {ProductId}", request.Id);
            return null;
        }

        _logger.LogInformation("Retrieved product: {ProductId}", request.Id);
        return product;
    }
}