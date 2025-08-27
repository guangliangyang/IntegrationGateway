namespace IntegrationGateway.Models.DTOs;

public class ProductHistoryDto
{
    public string ProductId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime LastModified { get; set; }
    public List<ProductVersionDto> Versions { get; set; } = new();
}

public class ProductVersionDto
{
    public int Version { get; set; }
    public DateTime ModifiedAt { get; set; }
    public string ModifiedBy { get; set; } = string.Empty;
}