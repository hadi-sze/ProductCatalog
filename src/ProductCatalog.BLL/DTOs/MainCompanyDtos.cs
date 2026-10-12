using System.ComponentModel.DataAnnotations;

namespace ProductCatalog.BLL.DTOs;

public sealed class MainCompanyWriteRequest
{
    [StringLength(200)]
    public string? Name { get; init; }

    [StringLength(64)]
    public string? UniqCode { get; init; }

    [StringLength(500)]
    public string? Address { get; init; }

    public bool IsActive { get; init; }
}

public sealed record MainCompanyResponse(int Id, string? Name, string? UniqCode, string? Address, bool IsActive);
