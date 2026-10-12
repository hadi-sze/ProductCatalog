namespace ProductCatalog.DAL.Exceptions;

public sealed class DuplicateCompanyCodeException(Exception innerException)
    : Exception("A main company with this uniqCode already exists.", innerException);
