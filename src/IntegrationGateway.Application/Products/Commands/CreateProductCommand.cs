using FluentValidation;
using IntegrationGateway.Application.Common.Behaviours;
using IntegrationGateway.Models.DTOs;
using IntegrationGateway.Services.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IntegrationGateway.Application.Products.Commands;

/// <summary>
/// Command to create a new product
/// </summary>
[Authorize]
public record CreateProductCommand : IRequest<ProductDto>, ICacheInvalidating
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required decimal Price { get; init; }
    public required string Category { get; init; }
    public bool IsActive { get; init; } = true;

    public IEnumerable<string> GetCacheKeysToInvalidate()
    {
        // Invalidate all product list caches
        yield return "GetProductsQuery*";
        yield return "GetProductsV2Query*";
    }
}

/// <summary>
/// Validator for CreateProductCommand
/// </summary>
public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Product name is required")
            .MaximumLength(200)
            .WithMessage("Product name must not exceed 200 characters");

        RuleFor(x => x.Description)
            .MaximumLength(1000)
            .WithMessage("Description must not exceed 1000 characters");

        RuleFor(x => x.Price)
            .GreaterThan(0)
            .WithMessage("Price must be greater than 0")
            .LessThan(1000000)
            .WithMessage("Price must be less than 1,000,000");

        RuleFor(x => x.Category)
            .NotEmpty()
            .WithMessage("Category is required")
            .MaximumLength(100)
            .WithMessage("Category must not exceed 100 characters");
    }
}

/// <summary>
/// Handler for CreateProductCommand
/// </summary>
public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, ProductDto>
{
    private readonly IProductService _productService;
    private readonly ILogger<CreateProductCommandHandler> _logger;

    public CreateProductCommandHandler(IProductService productService, ILogger<CreateProductCommandHandler> logger)
    {
        _productService = productService;
        _logger = logger;
    }

    public async Task<ProductDto> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating product: {ProductName}", request.Name);

        var createRequest = new CreateProductRequest
        {
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            Category = request.Category,
            IsActive = request.IsActive
        };

        var product = await _productService.CreateProductAsync(createRequest, cancellationToken);

        _logger.LogInformation("Created product: {ProductId}", product.Id);

        return product;
    }
}