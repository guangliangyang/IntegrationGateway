namespace WarehouseStub.Models;

public class Stock
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public bool InStock { get; set; }
    public string? Location { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public int ReservedQuantity { get; set; }
    public int AvailableQuantity => Math.Max(0, Quantity - ReservedQuantity);
}

public class BulkStockResponse
{
    public List<Stock> Stocks { get; set; } = new();
    public List<string> NotFound { get; set; } = new();
}

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? ErrorMessage { get; set; }
    public string RequestId { get; set; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}