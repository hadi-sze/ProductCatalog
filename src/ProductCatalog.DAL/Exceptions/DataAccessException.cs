namespace ProductCatalog.DAL.Exceptions;

public sealed class DataAccessException(string message, Exception innerException)
    : Exception(message, innerException);

public sealed class DuplicateSkuException(Exception innerException)
    : Exception("A product with this SKU already exists.", innerException);

public sealed class PersistenceConflictException(Exception innerException)
    : Exception("The resource changed while the operation was in progress.", innerException);
