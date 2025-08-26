using System.ComponentModel.DataAnnotations;

namespace IntegrationGateway.Models;

public class Product
{
    public string Id { get; set; } = string.Empty;
    
    [Required]
    public string Name { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    
    [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0")]
    public decimal Price { get; set; }
    
    public string Category { get; set; } = string.Empty;
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Stock information from Warehouse
    public int StockQuantity { get; set; }
    
    public bool InStock { get; set; }
    
    public string? WarehouseLocation { get; set; }
    
    // Version for API evolution
    public string? Supplier { get; set; } // V2 field
    
    public string? Tags { get; set; } // V2 field
}