using ProductCatalog.DAL.Models;

namespace ProductCatalog.DAL.Repositories;

public sealed record ProductPage(IReadOnlyList<Product> Items, int TotalCount);

public interface IProductRepository
{
    Task<ProductPage> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<Product?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task AddAsync(Product product, CancellationToken cancellationToken);
    Task UpdateAsync(Product product, Guid expectedVersion, CancellationToken cancellationToken);
    Task DeleteAsync(Product product, CancellationToken cancellationToken);
}
