namespace IntegrationGateway.Api.Configuration.Authentication;

public class JwtOptions
{
    public const string SectionName = "Jwt";
    
    public string SecretKey { get; set; } = string.Empty;
    
    public string Issuer { get; set; } = string.Empty;
    
    public string Audience { get; set; } = string.Empty;
    
    public int ExpirationMinutes { get; set; } = 60;
    
    public bool ValidateIssuer { get; set; } = true;
    
    public bool ValidateAudience { get; set; } = true;
    
    public bool ValidateLifetime { get; set; } = true;
    
    public bool ValidateIssuerSigningKey { get; set; } = true;
}