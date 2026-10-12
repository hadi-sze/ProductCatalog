using ProductCatalog.DAL.Models;

namespace ProductCatalog.DAL.Repositories;

public sealed record MainCompanyPage(IReadOnlyList<MainCompany> Items, int TotalCount);

public interface IMainCompanyRepository
{
    Task<MainCompanyPage> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<MainCompany?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task AddAsync(MainCompany company, CancellationToken cancellationToken);
    Task UpdateAsync(MainCompany company, CancellationToken cancellationToken);
    Task DeleteAsync(MainCompany company, CancellationToken cancellationToken);
}
