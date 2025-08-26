using IntegrationGateway.Models.Common;

namespace IntegrationGateway.Services.Interfaces;

public interface IIdempotencyService
{
    Task<IdempotencyKey?> GetAsync(string key, string operation, string bodyHash, CancellationToken cancellationToken = default);
    
    Task SetAsync(IdempotencyKey idempotencyKey, CancellationToken cancellationToken = default);
    
    Task<bool> ExistsAsync(string key, string operation, string bodyHash, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// High-concurrency safe method to get or create idempotency operation with locking
    /// Returns (IsExisting, Operation) where IsExisting indicates if this was a duplicate request
    /// </summary>
    Task<(bool IsExisting, IdempotencyKey Operation)> GetOrCreateOperationAsync(
        string key, string operation, string bodyHash, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update operation with response data (called after business logic completes)
    /// </summary>
    Task UpdateOperationResponseAsync(
        string key, string operation, string bodyHash, string responseBody, int statusCode, 
        CancellationToken cancellationToken = default);
    
    string GenerateCompositeKey(string key, string operation, string bodyHash);
}