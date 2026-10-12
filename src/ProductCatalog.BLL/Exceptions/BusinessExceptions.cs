namespace ProductCatalog.BLL.Exceptions;

public sealed class BusinessValidationException(IDictionary<string, string[]> errors)
    : Exception("One or more validation errors occurred.")
{
    public IDictionary<string, string[]> Errors { get; } = errors;
}

public sealed class ProductNotFoundException(int id) : Exception($"Product {id} was not found.");
public sealed class ProductConflictException(string message) : Exception(message);
