namespace IntegrationGateway.Models.External;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    
    public T? Data { get; set; }
    
    public string? ErrorMessage { get; set; }
    
    public string RequestId { get; set; } = string.Empty;
    
    public DateTime Timestamp { get; set; }
}