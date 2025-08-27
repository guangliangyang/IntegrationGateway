using FluentValidation;
using IntegrationGateway.Application.Common.Behaviours;
using IntegrationGateway.Application.Common.Interfaces;
using IntegrationGateway.Models.DTOs;
using IntegrationGateway.Services.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IntegrationGateway.Application.Products.Commands;

/// <summary>
/// Command to update an existing product
/// </summary>
public record UpdateProductCommand : IRequest<ProductDto>, ICacheInvalidating
{
    public required string Id { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public decimal? Price { get; init; }
    public string? Category { get; init; }
    public bool? IsActive { get; init; }

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
/// Validator for UpdateProductCommand
/// </summary>
public class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Product ID is required")
            .MaximumLength(50)
            .WithMessage("Product ID must not exceed 50 characters");

        RuleFor(x => x.Name)
            .MaximumLength(200)
            .WithMessage("Product name must not exceed 200 characters")
            .When(x => !string.IsNullOrEmpty(x.Name));

        RuleFor(x => x.Description)
            .MaximumLength(1000)
            .WithMessage("Description must not exceed 1000 characters")
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.Price)
            .GreaterThan(0)
            .WithMessage("Price must be greater than 0")
            .LessThan(1000000)
            .WithMessage("Price must be less than 1,000,000")
            .When(x => x.Price.HasValue);

        RuleFor(x => x.Category)
            .MaximumLength(100)
            .WithMessage("Category must not exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.Category));
    }
}

/// <summary>
/// Handler for UpdateProductCommand
/// </summary>
public class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand, ProductDto>
{
    private readonly IProductService _productService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<UpdateProductCommandHandler> _logger;

    public UpdateProductCommandHandler(
        IProductService productService,
        ICurrentUserService currentUser,
        ILogger<UpdateProductCommandHandler> logger)
    {
        _productService = productService;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<ProductDto> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("User {UserId} updating product: {ProductId}", 
            _currentUser.UserId ?? "Unknown", request.Id);

        var updateRequest = new UpdateProductRequest
        {
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            Category = request.Category,
            IsActive = request.IsActive
        };

        var product = await _productService.UpdateProductAsync(request.Id, updateRequest, cancellationToken);

        _logger.LogInformation("Updated product: {ProductId}", request.Id);

        return product;
    }
}