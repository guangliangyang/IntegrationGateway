namespace IntegrationGateway.Models.External;

public class ErpProduct
{
    public string Id { get; set; } = string.Empty;
    
    public string Name { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    
    public decimal Price { get; set; }
    
    public string Category { get; set; } = string.Empty;
    
    public bool IsActive { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    public DateTime UpdatedAt { get; set; }
    
    public string? Supplier { get; set; }
    
    public string? Sku { get; set; }
}

public class ErpProductRequest
{
    public string Name { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    
    public decimal Price { get; set; }
    
    public string Category { get; set; } = string.Empty;
    
    public bool IsActive { get; set; } = true;
    
    public string? Supplier { get; set; }
    
    public string? Sku { get; set; }
}

public class ErpResponse<T>
{
    public bool Success { get; set; }
    
    public T? Data { get; set; }
    
    public string? ErrorMessage { get; set; }
    
    public string RequestId { get; set; } = string.Empty;
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}