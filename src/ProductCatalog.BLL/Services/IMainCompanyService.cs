using ProductCatalog.BLL.DTOs;

namespace ProductCatalog.BLL.Services;

public interface IMainCompanyService
{
    Task<PagedResult<MainCompanyResponse>> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<MainCompanyResponse?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<MainCompanyResponse> CreateAsync(MainCompanyWriteRequest request, CancellationToken cancellationToken);
    Task<MainCompanyResponse> UpdateAsync(int id, MainCompanyWriteRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(int id, CancellationToken cancellationToken);
}
