using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.Api.ErrorHandling;
using ProductCatalog.BLL;
using ProductCatalog.DAL;
using ProductCatalog.DAL.Data;

var builder = WebApplication.CreateBuilder(args);

// Console logging also works for containers and accounts without Windows Event Log access.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddBusinessLogic();
builder.Services.AddDataAccess(builder.Configuration.GetConnectionString("Catalog")
    ?? throw new InvalidOperationException("Connection string 'Catalog' is required."));

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    // Development only. Apply migrations explicitly when deploying to other environments.
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Database.MigrateAsync();
}
else
{
    app.UseHttpsRedirection();
}

app.MapControllers();
app.Run();

public partial class Program;
