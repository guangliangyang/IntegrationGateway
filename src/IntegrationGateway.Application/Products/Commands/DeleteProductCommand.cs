using FluentValidation;
using IntegrationGateway.Application.Common.Behaviours;
using IntegrationGateway.Services.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IntegrationGateway.Application.Products.Commands;

/// <summary>
/// Command to delete a product (soft delete)
/// </summary>
[Authorize]
public record DeleteProductCommand : IRequest<bool>, ICacheInvalidating
{
    public required string Id { get; init; }

    public IEnumerable<string> GetCacheKeysToInvalidate()
    {
        // Invalidate specific product cache and all product list caches
        yield return $"GetProductQuery_{Id}";
        yield return $"GetProductV2Query_{Id}";
        yield return "GetProductsQuery*";
        yield return "GetProductsV2Query*";
    }
}

/// <summary>
/// Validator for DeleteProductCommand
/// </summary>
public class DeleteProductCommandValidator : AbstractValidator<DeleteProductCommand>
{
    public DeleteProductCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Product ID is required")
            .MaximumLength(50)
            .WithMessage("Product ID must not exceed 50 characters");
    }
}

/// <summary>
/// Handler for DeleteProductCommand
/// </summary>
public class DeleteProductCommandHandler : IRequestHandler<DeleteProductCommand, bool>
{
    private readonly IProductService _productService;
    private readonly ILogger<DeleteProductCommandHandler> _logger;

    public DeleteProductCommandHandler(IProductService productService, ILogger<DeleteProductCommandHandler> logger)
    {
        _productService = productService;
        _logger = logger;
    }

    public async Task<bool> Handle(DeleteProductCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting product: {ProductId}", request.Id);

        var success = await _productService.DeleteProductAsync(request.Id, cancellationToken);

        if (success)
        {
            _logger.LogInformation("Deleted product: {ProductId}", request.Id);
        }
        else
        {
            _logger.LogWarning("Product not found for deletion: {ProductId}", request.Id);
        }

        return success;
    }
}