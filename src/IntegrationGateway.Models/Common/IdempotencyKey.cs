namespace IntegrationGateway.Models.Common;

public class IdempotencyKey
{
    public string Key { get; set; } = string.Empty;
    
    public string Operation { get; set; } = string.Empty;
    
    public string BodyHash { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public TimeSpan ExpiresIn { get; set; } = TimeSpan.FromHours(24);
    
    public string? ResponseBody { get; set; }
    
    public int? ResponseStatusCode { get; set; }
    
    public bool IsExpired => DateTime.UtcNow > CreatedAt.Add(ExpiresIn);
    
    public static string GenerateBodyHash(string body)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(body));
        return Convert.ToBase64String(hashBytes);
    }
    
    public string GetCompositeKey() => $"{Key}|{Operation}|{BodyHash}";
}