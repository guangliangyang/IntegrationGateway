namespace IntegrationGateway.Models.DTOs;

public class ErrorResponse
{
    public string Type { get; set; } = string.Empty;
    
    public string Title { get; set; } = string.Empty;
    
    public string Detail { get; set; } = string.Empty;
    
    public int Status { get; set; }
    
    public string TraceId { get; set; } = string.Empty;
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    public Dictionary<string, object> Extensions { get; set; } = new();
}

public class ValidationErrorResponse : ErrorResponse
{
    public Dictionary<string, string[]> Errors { get; set; } = new();
}