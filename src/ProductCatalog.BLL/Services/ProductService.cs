using System.ComponentModel.DataAnnotations;
using ProductCatalog.BLL.DTOs;
using ProductCatalog.BLL.Exceptions;
using ProductCatalog.DAL.Models;
using ProductCatalog.DAL.Repositories;

namespace ProductCatalog.BLL.Services;

public sealed class ProductService(IProductRepository repository) : IProductService
{
    public async Task<PagedResult<ProductResponse>> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (pageSize is < 1 or > 100) errors["pageSize"] = ["Page size must be between 1 and 100."];
        if (page < 1 || (pageSize > 0 && (long)(page - 1) * pageSize > int.MaxValue))
            errors["page"] = ["Page must be positive and within the supported range."];
        if (errors.Count > 0) throw new BusinessValidationException(errors);
        var result = await repository.GetPageAsync(page, pageSize, cancellationToken);
        return new PagedResult<ProductResponse>(result.Items.Select(Map).ToArray(), page, pageSize, result.TotalCount);
    }

    public async Task<ProductResponse?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var product = await repository.GetByIdAsync(id, cancellationToken);
        return product is null ? null : Map(product);
    }

    public async Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken)
    {
        Validate(request);
        var product = new Product();
        Apply(request, product);
        await repository.AddAsync(product, cancellationToken);
        return Map(product);
    }

    public async Task<ProductResponse> UpdateAsync(int id, UpdateProductRequest request, CancellationToken cancellationToken)
    {
        Validate(request);
        if (request.Version is null || request.Version == Guid.Empty)
            throw new BusinessValidationException(new Dictionary<string, string[]>
                { ["Version"] = ["The current, non-empty product version is required."] });
        var product = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new ProductNotFoundException(id);
        if (product.Version != request.Version)
            throw new ProductConflictException("The product has changed. Fetch it again before updating.");
        Apply(request, product);
        await repository.UpdateAsync(product, request.Version.Value, cancellationToken);
        return Map(product);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var product = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new ProductNotFoundException(id);
        await repository.DeleteAsync(product, cancellationToken);
    }

    private static void Validate(ProductWriteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        var errors = results.SelectMany(r => r.MemberNames.DefaultIfEmpty("request")
                .Select(member => (Member: member, Error: r.ErrorMessage ?? "Invalid value.")))
            .GroupBy(r => r.Member).ToDictionary(g => g.Key, g => g.Select(r => r.Error).ToArray());
        var sku = request.Sku?.Trim().ToUpperInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(request.Name)) errors["Name"] = ["Name must contain non-whitespace characters."];
        if (sku.Length == 0 || sku.Any(c => !(c is >= 'A' and <= 'Z' or >= '0' and <= '9' or '-')))
            errors["Sku"] = ["SKU must contain only letters A-Z, digits, or hyphens."];
        if (request.Price is decimal price && decimal.Round(price, 2) != price)
            errors["Price"] = ["Price must have no more than two decimal places."];
        if (errors.Count > 0) throw new BusinessValidationException(errors);
    }

    private static void Apply(ProductWriteRequest request, Product product)
    {
        product.Name = request.Name.Trim();
        product.Sku = request.Sku.Trim().ToUpperInvariant();
        product.Price = request.Price!.Value;
        product.StockQuantity = request.StockQuantity!.Value;
    }

    private static ProductResponse Map(Product product) => new(product.Id, product.Name, product.Sku,
        product.Price, product.StockQuantity, product.Version);
}
