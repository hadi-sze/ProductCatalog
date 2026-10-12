using ProductCatalog.BLL.DTOs;

namespace ProductCatalog.BLL.Services;

public interface IProductService
{
    Task<PagedResult<ProductResponse>> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<ProductResponse?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken);
    Task<ProductResponse> UpdateAsync(int id, UpdateProductRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(int id, CancellationToken cancellationToken);
}
