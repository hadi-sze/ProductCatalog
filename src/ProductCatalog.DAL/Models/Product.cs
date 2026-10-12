namespace ProductCatalog.DAL.Models;

public sealed class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    // Application-managed token works with SQLite, which has no SQL Server rowversion.
    public Guid Version { get; set; } = Guid.NewGuid();
}
