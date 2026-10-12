# Product Catalog — .NET 10 Three-Tier REST API

A runnable controller-based CRUD service using .NET 10, EF Core 10.0.12, and SQLite. The product model and schema are defined in C#; the checked-in EF Core migration creates the database (Code First).

## Architecture

```text
HTTP → ProductsController → IProductService / ProductService
                         → IProductRepository / ProductRepository
                         → CatalogDbContext → SQLite

ProductCatalog/
├── ProductCatalog.sln
├── Directory.Build.props
├── global.json
├── NuGet.Config
├── .config/dotnet-tools.json
├── src/
│   ├── ProductCatalog.Api/                 Presentation layer
│   │   ├── Controllers/ProductsController.cs
│   │   ├── ErrorHandling/ApiExceptionHandler.cs
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   └── Properties/launchSettings.json
│   ├── ProductCatalog.BLL/                 Business logic layer
│   │   ├── DTOs/ProductDtos.cs
│   │   ├── Exceptions/BusinessExceptions.cs
│   │   ├── Services/IProductService.cs
│   │   ├── Services/ProductService.cs
│   │   └── DependencyInjection.cs
│   └── ProductCatalog.DAL/                 Data access layer
│       ├── Models/Product.cs
│       ├── Data/CatalogDbContext.cs
│       ├── Repositories/IProductRepository.cs
│       ├── Repositories/ProductRepository.cs
│       ├── Exceptions/DataAccessException.cs
│       ├── Migrations/
│       └── DependencyInjection.cs
├── tests/ProductCatalog.IntegrationTests/
└── requests/products.http
```

Each layer has its own `.csproj` targeting `net10.0`. Controllers depend on BLL interfaces and DTOs. The BLL owns validation, normalization, mapping, and operation orchestration; it consumes the DAL repository interface. The DAL owns POCO entities, EF mappings, database constraints, persistence, and database exception translation. `Program.cs` references the DAL to register dependencies and apply development migrations; controllers never use `DbContext` directly.

## Run

Install the .NET 10 SDK. No database server is needed. Run from the directory containing `ProductCatalog.sln`:

```powershell
dotnet restore ProductCatalog.sln --configfile NuGet.Config
dotnet build ProductCatalog.sln --no-restore
dotnet run --project src/ProductCatalog.Api --no-build --launch-profile http
```

The API listens at `http://localhost:5080`. In Development, startup applies the checked-in migrations and creates `catalog.db`. The OpenAPI JSON document is available at `http://localhost:5080/openapi/v1.json`; an interactive Swagger UI is not included. Use the supplied `.http` file in an HTTP-capable editor or the PowerShell example below.

For HTTPS, run `dotnet dev-certs https --trust`, then use `--launch-profile https`; the HTTPS URL is `https://localhost:7080`.

## CRUD endpoints

| Method | Path | Success | Other responses |
| --- | --- | --- | --- |
| GET | `/api/products?page=1&pageSize=20` | 200, paged DTOs | 400 invalid pagination |
| GET | `/api/products/{id}` | 200, product DTO | 404 missing product |
| POST | `/api/products` | 201, product DTO and `Location` | 400 validation, 409 duplicate SKU |
| PUT | `/api/products/{id}` | 200, updated product DTO | 400 validation, 404 missing, 409 duplicate SKU or stale version |
| DELETE | `/api/products/{id}` | 204 | 404 missing, 409 concurrent modification during deletion |

IDs must be positive integers; invalid route IDs return 404. Unexpected failures return 500 with a safe ProblemDetails body. JSON/model binding failures and validation errors return 400. Unsupported content types return 415.

Example, while the API is running:

```powershell
$baseUrl = 'http://localhost:5080/api/products'
$body = @{ name = 'Keyboard'; sku = 'KB-001'; price = 49.95; stockQuantity = 10 } | ConvertTo-Json
$product = Invoke-RestMethod $baseUrl -Method Post -ContentType 'application/json' -Body $body

Invoke-RestMethod $baseUrl
Invoke-RestMethod "$baseUrl/$($product.id)"

$update = @{ name = 'Mechanical Keyboard'; sku = 'KB-001'; price = 69.90; stockQuantity = 8; version = $product.version } | ConvertTo-Json
$product = Invoke-RestMethod "$baseUrl/$($product.id)" -Method Put -ContentType 'application/json' -Body $update

Invoke-RestMethod "$baseUrl/$($product.id)" -Method Delete
```

POST accepts `name`, `sku`, `price`, and `stockQuantity`. PUT fully replaces these fields and requires the `version` returned by the latest GET/POST/PUT. IDs and new versions are server-controlled. A stale version returns 409; fetch the product again before retrying. DELETE uses the version loaded by the repository to detect a concurrent change during that request; it does not accept a caller-supplied version.

The collection response contains `items`, `page`, `pageSize`, and `totalCount`. Items are ordered by ID. Page size is 1–100. The count and page are separate queries, so they may reflect adjacent database states during concurrent writes.

## Validation and persistence

- Names are required, limited to 120 characters, and trimmed.
- SKUs are required, limited to 32 characters, trimmed, converted to uppercase, and restricted to A–Z, digits, and hyphens. The database unique index prevents duplicate SKUs, including concurrent creates.
- Price and stock quantity must be supplied. Price is 0–1,000,000 with at most two decimal places; stock is a nonnegative 32-bit integer.
- SQLite stores price as integer cents through an EF value converter, retaining exact monetary values. Database check constraints enforce essential invariants.
- The application generates a GUID concurrency token for each version. EF includes the expected token in UPDATE/DELETE conditions and translates competing writes to 409.
- DTO annotations provide automatic `[ApiController]` validation; the BLL also validates requests for callers outside MVC.
- Dependencies are scoped to each request. Repository methods use asynchronous EF operations and pass cancellation tokens through all layers.

## Connection strings and Code First migrations

The default connection string in `src/ProductCatalog.Api/appsettings.json` is `Data Source=catalog.db`. Relative SQLite paths resolve against the process working directory. For a predictable location, use an absolute path:

```powershell
$env:ConnectionStrings__Catalog = 'Data Source=C:/data/product-catalog/catalog.db'
```

The parent directory must exist and be writable. This example path is a placeholder. Remove the override with `Remove-Item Env:ConnectionStrings__Catalog` when finished.

Restore the pinned local EF CLI tool:

```powershell
dotnet tool restore --configfile NuGet.Config
```

After changing a model or its configuration, add a migration and apply it:

```powershell
dotnet ef migrations add DescribeYourChange --project src/ProductCatalog.DAL --startup-project src/ProductCatalog.Api --output-dir Migrations
dotnet ef database update --project src/ProductCatalog.DAL --startup-project src/ProductCatalog.Api
```

The initial migration and model snapshot are already included; do not scaffold `InitialCreate` again. Startup uses `MigrateAsync`, never `EnsureCreated`. Outside Development, startup does not change the schema. For deployment, apply migrations as a controlled deployment step, or generate and execute a migration bundle against the intended SQLite database:

```powershell
dotnet ef migrations bundle --project src/ProductCatalog.DAL --startup-project src/ProductCatalog.Api --output efbundle
```

Changing the database provider requires changing its NuGet package, the `UseSqlite` registration and money mapping where appropriate, provider-specific error translation/check constraints, and regenerating migrations for that provider.

## Error handling

`ApiExceptionHandler` handles business validation (400), missing products (404), uniqueness and concurrency conflicts (409), and unexpected exceptions (500). Validation responses contain an `errors` dictionary. Error responses use ProblemDetails and include a trace ID; unexpected exception details are logged on the server and never returned to clients. DAL write failures are wrapped with their original exception; read failures propagate to the central handler.

```json
{
  "title": "Product conflict",
  "status": 409,
  "detail": "A product with this SKU already exists.",
  "instance": "/api/products",
  "traceId": "..."
}
```

## Verify

```powershell
dotnet run --project tests/ProductCatalog.IntegrationTests --no-build
dotnet ef migrations has-pending-model-changes --project src/ProductCatalog.DAL --startup-project src/ProductCatalog.Api
```

The integration executable exits nonzero on failure. It starts the real API as a hidden child process on a temporary loopback port and uses a separate temporary SQLite database, applying actual migrations. It checks CRUD status codes and persistence, DTO normalization, pagination, malformed/missing input, numeric constraints, duplicate/racing SKUs, stale versions, repository concurrency, BLL validation without MVC, safe 500 responses, and development OpenAPI. It stops its own process and removes its own database afterwards. The main catalog database is untouched. No additional test framework packages are required.

This is an architecture demonstration with anonymous endpoints. Configure authentication/authorization, hosting-specific HTTPS/proxy settings, backups, and deployment configuration before exposing a real catalog publicly.

## References

- [EF Core 10 release and .NET compatibility](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-10.0/whatsnew)
- [EF Core SQLite 10.0.12 package](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.Sqlite/10.0.12)
- [EF Core migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/)
- [ASP.NET Core error handling](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling?view=aspnetcore-10.0)
