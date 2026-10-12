using System.ComponentModel.DataAnnotations;
using ProductCatalog.BLL.DTOs;
using ProductCatalog.BLL.Exceptions;
using ProductCatalog.DAL.Models;
using ProductCatalog.DAL.Repositories;

namespace ProductCatalog.BLL.Services;

public sealed class MainCompanyService(IMainCompanyRepository repository) : IMainCompanyService
{
    public async Task<PagedResult<MainCompanyResponse>> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (pageSize is < 1 or > 100) errors["pageSize"] = ["Page size must be between 1 and 100."];
        if (page < 1 || (pageSize > 0 && (long)(page - 1) * pageSize > int.MaxValue))
            errors["page"] = ["Page must be positive and within the supported range."];
        if (errors.Count > 0) throw new BusinessValidationException(errors);
        var result = await repository.GetPageAsync(page, pageSize, cancellationToken);
        return new PagedResult<MainCompanyResponse>(result.Items.Select(Map).ToArray(), page, pageSize, result.TotalCount);
    }

    public async Task<MainCompanyResponse?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var company = await repository.GetByIdAsync(id, cancellationToken);
        return company is null ? null : Map(company);
    }

    public async Task<MainCompanyResponse> CreateAsync(MainCompanyWriteRequest request, CancellationToken cancellationToken)
    {
        Validate(request);
        var company = new MainCompany();
        Apply(request, company);
        await repository.AddAsync(company, cancellationToken);
        return Map(company);
    }

    public async Task<MainCompanyResponse> UpdateAsync(int id, MainCompanyWriteRequest request, CancellationToken cancellationToken)
    {
        Validate(request);
        var company = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new MainCompanyNotFoundException(id);
        Apply(request, company);
        await repository.UpdateAsync(company, cancellationToken);
        return Map(company);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var company = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new MainCompanyNotFoundException(id);
        await repository.DeleteAsync(company, cancellationToken);
    }

    private static void Validate(MainCompanyWriteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        var errors = results.SelectMany(r => r.MemberNames.DefaultIfEmpty("request")
                .Select(member => (Member: member, Error: r.ErrorMessage ?? "Invalid value.")))
            .GroupBy(r => r.Member).ToDictionary(g => g.Key, g => g.Select(r => r.Error).ToArray());
        // Unicode casing can expand a code, so validate the persisted form too.
        if (NormalizeCode(request.UniqCode)?.Length > 64)
            errors["UniqCode"] = ["The normalized uniqCode must be no longer than 64 characters."];
        if (errors.Count > 0) throw new BusinessValidationException(errors);
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? NormalizeCode(string? value) => Normalize(value)?.ToUpperInvariant();

    private static void Apply(MainCompanyWriteRequest request, MainCompany company)
    {
        company.Name = Normalize(request.Name);
        company.UniqCode = NormalizeCode(request.UniqCode);
        company.Address = Normalize(request.Address);
        company.IsActive = request.IsActive;
    }

    private static MainCompanyResponse Map(MainCompany company) =>
        new(company.Id, company.Name, company.UniqCode, company.Address, company.IsActive);
}
