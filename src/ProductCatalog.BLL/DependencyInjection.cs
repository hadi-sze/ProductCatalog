using Microsoft.Extensions.DependencyInjection;
using ProductCatalog.BLL.Services;

namespace ProductCatalog.BLL;

public static class DependencyInjection
{
    public static IServiceCollection AddBusinessLogic(this IServiceCollection services)
    {
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IMainCompanyService, MainCompanyService>();
        return services;
    }
}
