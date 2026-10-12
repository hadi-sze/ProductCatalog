using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.DAL.Data;
using ProductCatalog.DAL.Exceptions;
using ProductCatalog.DAL.Models;

namespace ProductCatalog.DAL.Repositories;

public sealed class MainCompanyRepository(CatalogDbContext db) : IMainCompanyRepository
{
    public async Task<MainCompanyPage> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.MainCompanies.AsNoTracking();
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(c => c.Id).Skip((page - 1) * pageSize)
            .Take(pageSize).ToListAsync(cancellationToken);
        return new MainCompanyPage(items, total);
    }

    public Task<MainCompany?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        db.MainCompanies.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task AddAsync(MainCompany company, CancellationToken cancellationToken)
    {
        db.MainCompanies.Add(company);
        await SaveAsync(cancellationToken);
    }

    public Task UpdateAsync(MainCompany company, CancellationToken cancellationToken)
    {
        // Retain the original tracked values for the concurrency check, even for an unchanged PUT.
        db.Entry(company).State = EntityState.Modified;
        return SaveAsync(cancellationToken);
    }

    public async Task DeleteAsync(MainCompany company, CancellationToken cancellationToken)
    {
        db.MainCompanies.Remove(company);
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
            throw new DuplicateCompanyCodeException(exception);
        }
        catch (DbUpdateException exception)
        {
            throw new DataAccessException("Unable to save main company changes.", exception);
        }
    }
}
