using Microsoft.EntityFrameworkCore;
using ProductCatalog.DAL.Models;

namespace ProductCatalog.DAL.Data;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var product = modelBuilder.Entity<Product>();
        product.ToTable("Products", table =>
        {
            table.HasCheckConstraint("CK_Products_Name", "length(trim(Name)) BETWEEN 1 AND 120");
            table.HasCheckConstraint("CK_Products_Sku", "length(Sku) BETWEEN 1 AND 32 AND Sku NOT GLOB '*[^A-Z0-9-]*'");
            table.HasCheckConstraint("CK_Products_Price", "Price BETWEEN 0 AND 100000000");
            table.HasCheckConstraint("CK_Products_StockQuantity", "StockQuantity >= 0");
        });
        product.HasKey(p => p.Id);
        product.Property(p => p.Name).HasMaxLength(120).IsRequired();
        product.Property(p => p.Sku).HasMaxLength(32).IsRequired();
        product.HasIndex(p => p.Sku).IsUnique();
        // Store money as integer cents to avoid SQLite decimal comparison limitations.
        product.Property(p => p.Price).HasConversion(
            value => (long)(value * 100m), value => value / 100m);
        product.Property(p => p.Version).IsConcurrencyToken();
    }
}
