using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.DAL.Data;
using ProductCatalog.DAL.Exceptions;
using ProductCatalog.DAL.Models;

namespace ProductCatalog.DAL.Repositories;

public sealed class ProductRepository(CatalogDbContext db) : IProductRepository
{
    public async Task<ProductPage> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.Products.AsNoTracking();
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(p => p.Id).Skip((page - 1) * pageSize)
            .Take(pageSize).ToListAsync(cancellationToken);
        return new ProductPage(items, totalCount);
    }

    public Task<Product?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        db.Products.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task AddAsync(Product product, CancellationToken cancellationToken)
    {
        db.Products.Add(product);
        await SaveAsync(cancellationToken);
    }

    public async Task UpdateAsync(Product product, Guid expectedVersion, CancellationToken cancellationToken)
    {
        db.Entry(product).Property(p => p.Version).OriginalValue = expectedVersion;
        product.Version = Guid.NewGuid();
        await SaveAsync(cancellationToken);
    }

    public async Task DeleteAsync(Product product, CancellationToken cancellationToken)
    {
        db.Products.Remove(product);
        await SaveAsync(cancellationToken);
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new PersistenceConflictException(exception);
        }
        catch (DbUpdateException exception) when
            (exception.InnerException is SqliteException { SqliteExtendedErrorCode: 2067 })
        {
            // The only unique index is SKU. Enforced in the database to cover concurrent creates.
            throw new DuplicateSkuException(exception);
        }
        catch (DbUpdateException exception)
        {
            throw new DataAccessException("Unable to save product changes.", exception);
        }
    }
}
