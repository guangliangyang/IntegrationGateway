using IntegrationGateway.Models.External;

namespace IntegrationGateway.Services.Interfaces;

public interface IErpService
{
    Task<ErpResponse<ErpProduct>> GetProductAsync(string productId, CancellationToken cancellationToken = default);
    
    Task<ErpResponse<List<ErpProduct>>> GetProductsAsync(CancellationToken cancellationToken = default);
    
    Task<ErpResponse<ErpProduct>> CreateProductAsync(ErpProductRequest request, CancellationToken cancellationToken = default);
    
    Task<ErpResponse<ErpProduct>> UpdateProductAsync(string productId, ErpProductRequest request, CancellationToken cancellationToken = default);
    
    Task<ErpResponse<bool>> DeleteProductAsync(string productId, CancellationToken cancellationToken = default);
}