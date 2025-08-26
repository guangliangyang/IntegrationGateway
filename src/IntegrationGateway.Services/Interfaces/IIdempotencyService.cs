using IntegrationGateway.Models.Common;

namespace IntegrationGateway.Services.Interfaces;

public interface IIdempotencyService
{
    Task<IdempotencyKey?> GetAsync(string key, string operation, string bodyHash, CancellationToken cancellationToken = default);
    
    Task SetAsync(IdempotencyKey idempotencyKey, CancellationToken cancellationToken = default);
    
    Task<bool> ExistsAsync(string key, string operation, string bodyHash, CancellationToken cancellationToken = default);
    
    string GenerateCompositeKey(string key, string operation, string bodyHash);
}