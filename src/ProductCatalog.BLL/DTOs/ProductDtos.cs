using System.ComponentModel.DataAnnotations;

namespace ProductCatalog.BLL.DTOs;

public class ProductWriteRequest
{
    [Required, StringLength(120)]
    public string Name { get; init; } = string.Empty;

    [Required, StringLength(32)]
    public string Sku { get; init; } = string.Empty;

    [Required, Range(typeof(decimal), "0", "1000000")]
    public decimal? Price { get; init; }

    [Required, Range(0, int.MaxValue)]
    public int? StockQuantity { get; init; }
}

public sealed class CreateProductRequest : ProductWriteRequest;

public sealed class UpdateProductRequest : ProductWriteRequest
{
    [Required]
    public Guid? Version { get; init; }
}

public sealed record ProductResponse(int Id, string Name, string Sku, decimal Price,
    int StockQuantity, Guid Version);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
