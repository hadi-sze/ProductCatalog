namespace ProductCatalog.BLL.Exceptions;

public sealed class MainCompanyNotFoundException(int id) : Exception($"Main company {id} was not found.");
