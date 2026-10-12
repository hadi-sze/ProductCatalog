namespace ProductCatalog.DAL.Models;

public sealed class MainCompany
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? UniqCode { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; }
}
