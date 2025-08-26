using IntegrationGateway.Models.External;

namespace IntegrationGateway.Services.Interfaces;

public interface IWarehouseService
{
    Task<WarehouseResponse<WarehouseStock>> GetStockAsync(string productId, CancellationToken cancellationToken = default);
    
    Task<WarehouseResponse<BulkStockResponse>> GetBulkStockAsync(List<string> productIds, CancellationToken cancellationToken = default);
}