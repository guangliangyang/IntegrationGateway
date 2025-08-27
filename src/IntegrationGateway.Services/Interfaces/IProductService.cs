using IntegrationGateway.Models;
using IntegrationGateway.Models.DTOs;

namespace IntegrationGateway.Services.Interfaces;

public interface IProductService
{
    Task<ProductListResponse> GetProductsAsync(int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);
    
    Task<ProductListV2Response> GetProductsV2Async(int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);
    
    Task<ProductDto?> GetProductAsync(string productId, CancellationToken cancellationToken = default);
    
    Task<ProductV2Dto?> GetProductV2Async(string productId, CancellationToken cancellationToken = default);
    
    Task<ProductDto> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken = default);
    
    Task<ProductDto> UpdateProductAsync(string productId, UpdateProductRequest request, CancellationToken cancellationToken = default);
    
    Task<bool> DeleteProductAsync(string productId, CancellationToken cancellationToken = default);
}