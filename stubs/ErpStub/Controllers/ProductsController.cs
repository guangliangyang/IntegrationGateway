using Microsoft.AspNetCore.Mvc;
using ErpStub.Models;
using System.Collections.Concurrent;

namespace ErpStub.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly ILogger<ProductsController> _logger;
    private static readonly ConcurrentDictionary<string, Product> _products = new();

    static ProductsController()
    {
        // Initialize with sample data
        SeedData();
    }

    public ProductsController(ILogger<ProductsController> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<Product>>>> GetProducts()
    {
        _logger.LogInformation("ERP: Getting all products");
        
        // Simulate some processing time
        await Task.Delay(Random.Shared.Next(50, 200));
        
        // Simulate occasional failures for circuit breaker testing
        if (Random.Shared.Next(1, 100) <= 20) // 20% failure rate
        {
            _logger.LogWarning("ERP: Simulated service failure");
            return StatusCode(503, new ApiResponse<List<Product>>
            {
                Success = false,
                ErrorMessage = "Service temporarily unavailable",
                RequestId = Guid.NewGuid().ToString()
            });
        }

        var products = _products.Values.Where(p => p.IsActive).ToList();
        
        return Ok(new ApiResponse<List<Product>>
        {
            Success = true,
            Data = products,
            RequestId = Guid.NewGuid().ToString()
        });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<Product>>> GetProduct(string id)
    {
        _logger.LogInformation("ERP: Getting product {ProductId}", id);
        
        await Task.Delay(Random.Shared.Next(20, 100));
        
        if (!_products.TryGetValue(id, out var product))
        {
            _logger.LogWarning("ERP: Product not found {ProductId}", id);
            return NotFound(new ApiResponse<Product>
            {
                Success = false,
                ErrorMessage = "Product not found",
                RequestId = Guid.NewGuid().ToString()
            });
        }

        return Ok(new ApiResponse<Product>
        {
            Success = true,
            Data = product,
            RequestId = Guid.NewGuid().ToString()
        });
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<Product>>> CreateProduct([FromBody] ProductRequest request)
    {
        _logger.LogInformation("ERP: Creating product {ProductName}", request.Name);
        
        await Task.Delay(Random.Shared.Next(100, 300));
        
        // Validate request
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new ApiResponse<Product>
            {
                Success = false,
                ErrorMessage = "Product name is required",
                RequestId = Guid.NewGuid().ToString()
            });
        }

        var product = new Product
        {
            Id = Guid.NewGuid().ToString(),
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            Category = request.Category,
            IsActive = request.IsActive,
            Supplier = request.Supplier,
            Sku = request.Sku ?? GenerateSku(request.Category),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _products.TryAdd(product.Id, product);
        
        _logger.LogInformation("ERP: Created product {ProductId}", product.Id);
        
        return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, new ApiResponse<Product>
        {
            Success = true,
            Data = product,
            RequestId = Guid.NewGuid().ToString()
        });
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<Product>>> UpdateProduct(string id, [FromBody] ProductRequest request)
    {
        _logger.LogInformation("ERP: Updating product {ProductId}", id);
        
        await Task.Delay(Random.Shared.Next(100, 250));
        
        if (!_products.TryGetValue(id, out var existingProduct))
        {
            _logger.LogWarning("ERP: Product not found for update {ProductId}", id);
            return NotFound(new ApiResponse<Product>
            {
                Success = false,
                ErrorMessage = "Product not found",
                RequestId = Guid.NewGuid().ToString()
            });
        }

        // Update product
        existingProduct.Name = request.Name;
        existingProduct.Description = request.Description;
        existingProduct.Price = request.Price;
        existingProduct.Category = request.Category;
        existingProduct.IsActive = request.IsActive;
        existingProduct.Supplier = request.Supplier;
        existingProduct.Sku = request.Sku ?? existingProduct.Sku;
        existingProduct.UpdatedAt = DateTime.UtcNow;

        _logger.LogInformation("ERP: Updated product {ProductId}", id);
        
        return Ok(new ApiResponse<Product>
        {
            Success = true,
            Data = existingProduct,
            RequestId = Guid.NewGuid().ToString()
        });
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteProduct(string id)
    {
        _logger.LogInformation("ERP: Deleting product {ProductId}", id);
        
        await Task.Delay(Random.Shared.Next(50, 150));
        
        if (!_products.TryGetValue(id, out var product))
        {
            _logger.LogWarning("ERP: Product not found for deletion {ProductId}", id);
            return NotFound(new ApiResponse<bool>
            {
                Success = false,
                ErrorMessage = "Product not found",
                RequestId = Guid.NewGuid().ToString()
            });
        }

        // Soft delete - just mark as inactive
        product.IsActive = false;
        product.UpdatedAt = DateTime.UtcNow;
        
        _logger.LogInformation("ERP: Deleted (soft) product {ProductId}", id);
        
        return Ok(new ApiResponse<bool>
        {
            Success = true,
            Data = true,
            RequestId = Guid.NewGuid().ToString()
        });
    }

    private static void SeedData()
    {
        var sampleProducts = new[]
        {
            new Product { Id = "prod-001", Name = "Premium Laptop", Description = "High-performance business laptop", Price = 1299.99m, Category = "Electronics", Supplier = "TechSupplier Inc", Sku = "ELEC-LAP-001" },
            new Product { Id = "prod-002", Name = "Wireless Headphones", Description = "Noise-cancelling wireless headphones", Price = 199.99m, Category = "Electronics", Supplier = "TechSupplier Inc", Sku = "ELEC-HEAD-002" },
            new Product { Id = "prod-003", Name = "Business Shirt", Description = "Cotton blend business shirt", Price = 49.99m, Category = "Clothing", Supplier = "Fashion Wholesale Ltd", Sku = "CLTH-SHRT-003" },
            new Product { Id = "prod-004", Name = "Programming Guide", Description = "Complete programming reference", Price = 79.99m, Category = "Books", Supplier = "BookDistributor Co", Sku = "BOOK-PROG-004" },
            new Product { Id = "prod-005", Name = "Organic Coffee", Description = "Fair trade organic coffee beans", Price = 24.99m, Category = "Food", Supplier = "Fresh Foods Supply", Sku = "FOOD-COFF-005" }
        };

        foreach (var product in sampleProducts)
        {
            _products.TryAdd(product.Id, product);
        }
    }

    private static string GenerateSku(string category)
    {
        var prefix = category.ToUpperInvariant() switch
        {
            "ELECTRONICS" => "ELEC",
            "CLOTHING" => "CLTH",
            "BOOKS" => "BOOK",
            "FOOD" => "FOOD",
            _ => "MISC"
        };
        
        return $"{prefix}-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
    }
}