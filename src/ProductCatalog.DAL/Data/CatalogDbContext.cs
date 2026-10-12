using Microsoft.EntityFrameworkCore;
using ProductCatalog.DAL.Models;

namespace ProductCatalog.DAL.Data;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<MainCompany> MainCompanies => Set<MainCompany>();

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

        var company = modelBuilder.Entity<MainCompany>();
        company.ToTable("MainCompanies", table =>
        {
            table.HasCheckConstraint("CK_MainCompanies_Name", "Name IS NULL OR length(Name) <= 200");
            table.HasCheckConstraint("CK_MainCompanies_UniqCode", "UniqCode IS NULL OR length(UniqCode) BETWEEN 1 AND 64");
            table.HasCheckConstraint("CK_MainCompanies_Address", "Address IS NULL OR length(Address) <= 500");
            table.HasCheckConstraint("CK_MainCompanies_IsActive", "IsActive IN (0, 1)");
        });
        company.HasKey(c => c.Id);
        company.Property(c => c.Id).ValueGeneratedOnAdd();
        company.Property(c => c.Name).HasMaxLength(200).IsConcurrencyToken();
        company.Property(c => c.UniqCode).HasMaxLength(64).IsConcurrencyToken();
        company.Property(c => c.Address).HasMaxLength(500).IsConcurrencyToken();
        company.Property(c => c.IsActive).IsConcurrencyToken();
        company.HasIndex(c => c.UniqCode).IsUnique();
    }
}
