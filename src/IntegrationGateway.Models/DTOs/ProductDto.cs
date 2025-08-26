using System.ComponentModel.DataAnnotations;

namespace IntegrationGateway.Models.DTOs;

public class ProductDto
{
    public string Id { get; set; } = string.Empty;
    
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(1000)]
    public string? Description { get; set; }
    
    [Range(0.01, 999999.99)]
    public decimal Price { get; set; }
    
    [Required]
    [StringLength(100)]
    public string Category { get; set; } = string.Empty;
    
    public bool IsActive { get; set; } = true;
    
    public int StockQuantity { get; set; }
    
    public bool InStock { get; set; }
    
    public string? WarehouseLocation { get; set; }
}

public class ProductV2Dto : ProductDto
{
    public string? Supplier { get; set; }
    
    public List<string> Tags { get; set; } = new();
    
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class CreateProductRequest
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(1000)]
    public string? Description { get; set; }
    
    [Range(0.01, 999999.99)]
    public decimal Price { get; set; }
    
    [Required]
    [StringLength(100)]
    public string Category { get; set; } = string.Empty;
    
    public bool IsActive { get; set; } = true;
}

public class UpdateProductRequest
{
    [StringLength(200, MinimumLength = 1)]
    public string? Name { get; set; }
    
    [StringLength(1000)]
    public string? Description { get; set; }
    
    [Range(0.01, 999999.99)]
    public decimal? Price { get; set; }
    
    [StringLength(100)]
    public string? Category { get; set; }
    
    public bool? IsActive { get; set; }
}

public class ProductListResponse
{
    public List<ProductDto> Products { get; set; } = new();
    
    public int Total { get; set; }
    
    public int Page { get; set; } = 1;
    
    public int PageSize { get; set; } = 50;
}

public class ProductListV2Response
{
    public List<ProductV2Dto> Products { get; set; } = new();
    
    public int Total { get; set; }
    
    public int Page { get; set; } = 1;
    
    public int PageSize { get; set; } = 50;
    
    public Dictionary<string, object> Metadata { get; set; } = new();
}