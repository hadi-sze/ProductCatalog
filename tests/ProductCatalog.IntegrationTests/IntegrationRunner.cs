using System.Net;
using System.Net.Sockets;
using System.Net.Http.Json;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using ProductCatalog.BLL;
using ProductCatalog.DAL;
using ProductCatalog.BLL.DTOs;
using ProductCatalog.BLL.Exceptions;
using ProductCatalog.BLL.Services;
using ProductCatalog.DAL.Data;
using ProductCatalog.DAL.Exceptions;
using ProductCatalog.DAL.Repositories;
using ProductCatalog.DAL.Models;

internal static class IntegrationRunner
{
    private static int checks;

    public static async Task<int> Main()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"ProductCatalogTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            await CheckUpgradeAsync(Path.Combine(directory, "upgrade.db"));
            await RunAsync(Path.Combine(directory, "integration.db"));
            Console.WriteLine($"PASS: {checks} integration checks (real SQLite migrations and HTTP pipeline).");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
        finally
        {
            // Only this generated test directory is removed, after the factory closes connections.
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task CheckUpgradeAsync(string databasePath)
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseSqlite($"Data Source={databasePath};Pooling=False").Options;
        await using var db = new CatalogDbContext(options);
        await db.GetService<IMigrator>().MigrateAsync(db.Database.GetMigrations().First());
        var existing = new Product { Name = "Existing product", Sku = "EXISTING", Price = 12.34m, StockQuantity = 2 };
        db.Products.Add(existing);
        await db.SaveChangesAsync();
        await db.Database.MigrateAsync();
        db.ChangeTracker.Clear();
        var preserved = await db.Products.SingleAsync();
        Check(preserved.Id == existing.Id && preserved.Sku == existing.Sku && preserved.Price == existing.Price,
            "Main company migration preserves existing product data");
        Check(await db.MainCompanies.CountAsync() == 0, "Main company table is created when upgrading the initial schema");
    }

    private static async Task RunAsync(string databasePath)
    {
        await using var factory = await CatalogHost.StartAsync(databasePath);
        using var client = factory.CreateClient();
        var empty = await client.GetFromJsonAsync<PagedResult<ProductResponse>>("/api/products");
        Check(empty is { TotalCount: 0, Page: 1, PageSize: 20 }, "Fresh migrated database is empty");
        var openApi = await client.GetAsync("/openapi/v1.json");
        Check(openApi.IsSuccessStatusCode, "Development OpenAPI document is available");

        var create = await client.PostAsJsonAsync("/api/products", Write("  Keyboard  ", "kb-001", 49.95m, 10));
        Check(create.StatusCode == HttpStatusCode.Created, "POST returns 201");
        var product = (await create.Content.ReadFromJsonAsync<ProductResponse>())!;
        Check(product is { Name: "Keyboard", Sku: "KB-001", Price: 49.95m, StockQuantity: 10 }
            && product.Version != Guid.Empty, "Business normalization and money round-trip");
        Check(create.Headers.Location?.AbsolutePath == $"/api/products/{product.Id}", "POST returns the resource Location");
        var get = await client.GetFromJsonAsync<ProductResponse>(create.Headers.Location);
        Check(get == product, "GET reads the persisted product");

        await Expect(client.PostAsJsonAsync("/api/products", Write("Duplicate", "kb-001", 1m, 0)), HttpStatusCode.Conflict, "Duplicate SKU is rejected");
        await Expect(client.PostAsJsonAsync("/api/products", Write(" ", "BAD-1", 1m, 0)), HttpStatusCode.BadRequest, "Whitespace name is rejected");
        await Expect(client.PostAsJsonAsync("/api/products", Write("Bad", "BAD_1", 1m, 0)), HttpStatusCode.BadRequest, "Invalid SKU characters are rejected");
        await Expect(client.PostAsJsonAsync("/api/products", Write("Bad", "BAD-1", 1.001m, 0)), HttpStatusCode.BadRequest, "Fractional cents are rejected");
        await Expect(client.PostAsJsonAsync("/api/products", Write("Bad", "BAD-1", -1m, 0)), HttpStatusCode.BadRequest, "Negative price is rejected");
        await Expect(client.PostAsJsonAsync("/api/products", Write("Bad", "BAD-1", 1m, -1)), HttpStatusCode.BadRequest, "Negative stock is rejected");
        await Expect(client.PostAsJsonAsync("/api/products", new { name = "Missing", sku = "MISSING" }), HttpStatusCode.BadRequest, "Missing numeric fields are rejected");
        await Expect(client.PostAsync("/api/products", new StringContent("{", Encoding.UTF8, "application/json")), HttpStatusCode.BadRequest, "Malformed JSON is rejected");
        await Expect(client.GetAsync("/api/products?page=0"), HttpStatusCode.BadRequest, "Invalid page is rejected");
        await Expect(client.GetAsync("/api/products?pageSize=101"), HttpStatusCode.BadRequest, "Oversized page is rejected");
        await Expect(client.GetAsync("/api/products?page=2147483647&pageSize=100"), HttpStatusCode.BadRequest, "Pagination offset overflow is rejected");

        var update = new UpdateProductRequest { Name = "Mechanical Keyboard", Sku = "KB-001", Price = 69.90m, StockQuantity = 8, Version = product.Version };
        var put = await client.PutAsJsonAsync($"/api/products/{product.Id}", update);
        Check(put.StatusCode == HttpStatusCode.OK, "PUT returns 200");
        var updated = (await put.Content.ReadFromJsonAsync<ProductResponse>())!;
        Check(updated.Name == update.Name && updated.Price == 69.90m && updated.Version != product.Version, "PUT replaces values and rotates the version");
        await Expect(client.PutAsJsonAsync($"/api/products/{product.Id}", update), HttpStatusCode.Conflict, "Stale update is rejected");
        Check(await client.GetFromJsonAsync<ProductResponse>($"/api/products/{product.Id}") == updated, "Rejected stale update preserves the stored product");
        await Expect(client.PutAsJsonAsync($"/api/products/{product.Id}", new UpdateProductRequest
            { Name = "Missing version", Sku = "KB-001", Price = 1, StockQuantity = 0 }), HttpStatusCode.BadRequest, "Missing update version is rejected");
        await Expect(client.PutAsJsonAsync("/api/products/999999", update), HttpStatusCode.NotFound, "Updating a missing product returns 404");

        var secondResponse = await client.PostAsJsonAsync("/api/products", Write("Mouse", "MS-001", 0, 0));
        Check(secondResponse.StatusCode == HttpStatusCode.Created, "Zero price and zero stock are allowed");
        var second = (await secondResponse.Content.ReadFromJsonAsync<ProductResponse>())!;
        await Expect(client.PutAsJsonAsync($"/api/products/{second.Id}", new UpdateProductRequest
            { Name = "Mouse", Sku = "kb-001", Price = 5, StockQuantity = 0, Version = second.Version }), HttpStatusCode.Conflict, "Duplicate SKU update returns 409");
        Check(await client.GetFromJsonAsync<ProductResponse>($"/api/products/{second.Id}") == second, "Failed duplicate update is rolled back");
        var page = await client.GetFromJsonAsync<PagedResult<ProductResponse>>("/api/products?page=2&pageSize=1");
        Check(page is { TotalCount: 2, Page: 2, PageSize: 1 } && page.Items.Single().Id == second.Id, "Pagination has stable ordering and total count");

        // Two tracked copies model competing writers between the read and save steps.
        using (var firstScope = factory.Services.CreateScope())
        using (var secondScope = factory.Services.CreateScope())
        {
            var firstRepository = firstScope.ServiceProvider.GetRequiredService<IProductRepository>();
            var secondRepository = secondScope.ServiceProvider.GetRequiredService<IProductRepository>();
            var firstCopy = (await firstRepository.GetByIdAsync(product.Id, default))!;
            var secondCopy = (await secondRepository.GetByIdAsync(product.Id, default))!;
            var expectedVersion = firstCopy.Version;
            firstCopy.StockQuantity = 7;
            await firstRepository.UpdateAsync(firstCopy, expectedVersion, default);
            secondCopy.StockQuantity = 6;
            try
            {
                await secondRepository.UpdateAsync(secondCopy, expectedVersion, default);
                throw new InvalidOperationException("Expected repository concurrency conflict.");
            }
            catch (PersistenceConflictException)
            {
                Check(true, "Database concurrency token prevents a lost update after competing reads");
            }
        }
        using (var scope = factory.Services.CreateScope())
        {
            try
            {
                await scope.ServiceProvider.GetRequiredService<IProductService>()
                    .CreateAsync(Write("Bad", "BAD", 1.001m, 0), default);
                throw new InvalidOperationException("Expected business validation error.");
            }
            catch (BusinessValidationException exception)
            {
                Check(exception.Errors.ContainsKey("Price"), "BLL validates independently of controllers");
            }
        }

        var concurrent = await Task.WhenAll(
            client.PostAsJsonAsync("/api/products", Write("Race A", "RACE", 1m, 0)),
            client.PostAsJsonAsync("/api/products", Write("Race B", "race", 1m, 0)));
        Check(concurrent.Count(r => r.StatusCode == HttpStatusCode.Created) == 1
            && concurrent.Count(r => r.StatusCode == HttpStatusCode.Conflict) == 1,
            "Concurrent duplicate creates produce one success and one conflict");

        await Expect(client.DeleteAsync($"/api/products/{product.Id}"), HttpStatusCode.NoContent, "DELETE returns 204");
        await Expect(client.GetAsync($"/api/products/{product.Id}"), HttpStatusCode.NotFound, "Deleted product returns 404");
        await Expect(client.DeleteAsync($"/api/products/{product.Id}"), HttpStatusCode.NotFound, "Repeated DELETE returns 404");

        await CheckMainCompaniesAsync(client, factory.Services);

        // Simulate an infrastructure failure to verify that SQL details stay out of responses.
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Database.ExecuteSqlRawAsync("DROP TABLE Products");
        var failure = await client.GetAsync("/api/products");
        Check(failure.StatusCode == HttpStatusCode.InternalServerError, "Database failure returns 500");
        var failureJson = JsonDocument.Parse(await failure.Content.ReadAsStringAsync());
        Check(failure.Content.Headers.ContentType?.MediaType == "application/problem+json"
            && failureJson.RootElement.GetProperty("detail").GetString() == "The request could not be completed. Try again later."
            && failureJson.RootElement.TryGetProperty("traceId", out _), "500 response has safe ProblemDetails and trace ID");
        factory.Completed = true;
    }

    private static async Task CheckMainCompaniesAsync(HttpClient client, ServiceProvider services)
    {
        const string route = "/api/main-companies";
        var empty = await client.GetFromJsonAsync<PagedResult<MainCompanyResponse>>(route);
        Check(empty is { TotalCount: 0 }, "New main company table is empty");
        var create = await client.PostAsJsonAsync(route, new MainCompanyWriteRequest
            { Name = "  Main Company  ", UniqCode = " company-001 ", Address = "  12 Main Street  ", IsActive = true });
        Check(create.StatusCode == HttpStatusCode.Created, "Company POST returns 201");
        var company = (await create.Content.ReadFromJsonAsync<MainCompanyResponse>())!;
        Check(company.Id > 0 && company is { Name: "Main Company", UniqCode: "COMPANY-001", Address: "12 Main Street", IsActive: true }, "Company fields are saved and normalized");
        Check(create.Headers.Location?.AbsolutePath == $"{route}/{company.Id}", "Company Location header references the generated ID");
        Check(await client.GetFromJsonAsync<MainCompanyResponse>($"{route}/{company.Id}") == company, "Company GET reads persisted data");
        await Expect(client.PostAsJsonAsync(route, new { uniqCode = "company-001" }), HttpStatusCode.Conflict, "Company uniqCode uniqueness ignores letter casing");
        await Expect(client.PostAsJsonAsync(route, new { name = new string('N', 201) }), HttpStatusCode.BadRequest, "Company name length is validated");
        await Expect(client.PostAsJsonAsync(route, new { uniqCode = new string('C', 65) }), HttpStatusCode.BadRequest, "Company uniqCode length is validated");
        await Expect(client.PostAsJsonAsync(route, new { address = new string('A', 501) }), HttpStatusCode.BadRequest, "Company address length is validated");
        await Expect(client.PostAsJsonAsync(route, new { isActive = "yes" }), HttpStatusCode.BadRequest, "Company isActive must be a boolean");
        await Expect(client.GetAsync($"{route}?page=0"), HttpStatusCode.BadRequest, "Company pagination is validated");
        await Expect(client.GetAsync($"{route}?pageSize=101"), HttpStatusCode.BadRequest, "Company page size is bounded");
        await Expect(client.GetAsync($"{route}?page=2147483647&pageSize=100"), HttpStatusCode.BadRequest, "Company pagination cannot overflow");

        var optional = await client.PostAsJsonAsync(route, new { });
        Check(optional.StatusCode == HttpStatusCode.Created, "Company non-ID attributes may be omitted");
        var second = (await optional.Content.ReadFromJsonAsync<MainCompanyResponse>())!;
        Check(second.Id != company.Id && second is { Name: null, UniqCode: null, Address: null, IsActive: false }, "Omitted company attributes have explicit defaults");
        var anotherOptional = await client.PostAsJsonAsync(route, new { uniqCode = " " });
        Check(anotherOptional.StatusCode == HttpStatusCode.Created, "Multiple companies can have an unspecified uniqCode");
        var page = await client.GetFromJsonAsync<PagedResult<MainCompanyResponse>>($"{route}?page=2&pageSize=1");
        Check(page is { TotalCount: 3 } && page.Items.Single().Id == second.Id, "Company pagination uses stable ID ordering");

        var update = await client.PutAsJsonAsync($"{route}/{company.Id}", new MainCompanyWriteRequest
            { Name = "Updated Main Company", UniqCode = "company-001", Address = "34 Main Street", IsActive = false });
        Check(update.StatusCode == HttpStatusCode.OK, "Company PUT returns 200");
        var updated = (await update.Content.ReadFromJsonAsync<MainCompanyResponse>())!;
        Check(updated.Id == company.Id && updated is { Name: "Updated Main Company", Address: "34 Main Street", IsActive: false }, "Company PUT preserves ID and updates attributes");
        Check(await client.GetFromJsonAsync<MainCompanyResponse>($"{route}/{company.Id}") == updated, "Company PUT changes persist");
        await Expect(client.PutAsJsonAsync($"{route}/{second.Id}", new { uniqCode = "COMPANY-001" }), HttpStatusCode.Conflict, "Company duplicate code update returns 409");
        Check(await client.GetFromJsonAsync<MainCompanyResponse>($"{route}/{second.Id}") == second, "Rejected company update preserves data");
        await Expect(client.PutAsJsonAsync($"{route}/999999", new { }), HttpStatusCode.NotFound, "Company update handles a missing ID");

        using (var firstScope = services.CreateScope())
        using (var secondScope = services.CreateScope())
        {
            var first = firstScope.ServiceProvider.GetRequiredService<IMainCompanyRepository>();
            var next = secondScope.ServiceProvider.GetRequiredService<IMainCompanyRepository>();
            var firstCopy = (await first.GetByIdAsync(company.Id, default))!;
            var nextCopy = (await next.GetByIdAsync(company.Id, default))!;
            firstCopy.Address = "New address";
            await first.UpdateAsync(firstCopy, default);
            nextCopy.Name = "Concurrent edit";
            try
            {
                await next.UpdateAsync(nextCopy, default);
                throw new InvalidOperationException("Expected company concurrency conflict.");
            }
            catch (PersistenceConflictException) { Check(true, "Company competing writes detect a concurrency conflict"); }
        }
        using (var scope = services.CreateScope())
        {
            try
            {
                await scope.ServiceProvider.GetRequiredService<IMainCompanyService>().CreateAsync(
                    new MainCompanyWriteRequest { Address = new string('A', 501) }, default);
                throw new InvalidOperationException("Expected company BLL validation error.");
            }
            catch (BusinessValidationException exception)
            {
                Check(exception.Errors.ContainsKey("Address"), "Company BLL validates without MVC");
            }
        }
        var concurrent = await Task.WhenAll(
            client.PostAsJsonAsync(route, new { uniqCode = "COMPANY-RACE" }),
            client.PostAsJsonAsync(route, new { uniqCode = "company-race" }));
        Check(concurrent.Count(r => r.StatusCode == HttpStatusCode.Created) == 1
            && concurrent.Count(r => r.StatusCode == HttpStatusCode.Conflict) == 1, "Company concurrent duplicate creates are protected");
        await Expect(client.DeleteAsync($"{route}/{company.Id}"), HttpStatusCode.NoContent, "Company DELETE returns 204");
        await Expect(client.GetAsync($"{route}/{company.Id}"), HttpStatusCode.NotFound, "Deleted company returns 404");
        await Expect(client.DeleteAsync($"{route}/{company.Id}"), HttpStatusCode.NotFound, "Company repeated DELETE returns 404");

        using var openApi = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        Check(openApi.RootElement.GetProperty("paths").TryGetProperty(route, out var path)
            && path.TryGetProperty("get", out _) && path.TryGetProperty("post", out _), "OpenAPI includes company endpoints");
    }

    private static CreateProductRequest Write(string name, string sku, decimal price, int stock) =>
        new() { Name = name, Sku = sku, Price = price, StockQuantity = stock };

    private static async Task Expect(Task<HttpResponseMessage> pending, HttpStatusCode expected, string description)
    {
        using var response = await pending;
        var body = await response.Content.ReadAsStringAsync();
        Check(response.StatusCode == expected, $"{description}; received {(int)response.StatusCode}: {body}");
        if ((int)expected >= 400)
        {
            using var json = JsonDocument.Parse(body);
            Check(response.Content.Headers.ContentType?.MediaType == "application/problem+json"
                && json.RootElement.GetProperty("status").GetInt32() == (int)expected, $"{description}: ProblemDetails contract");
        }
    }

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException($"FAIL: {description}");
        checks++;
    }
}

internal sealed class CatalogHost : IAsyncDisposable
{
    private readonly Process process;
    private readonly Uri baseAddress;
    private readonly Task<string> output;
    private readonly Task<string> errors;
    public ServiceProvider Services { get; }
    public bool Completed { get; set; }

    private CatalogHost(Process process, Uri baseAddress, string connectionString)
    {
        this.process = process;
        this.baseAddress = baseAddress;
        output = process.StandardOutput.ReadToEndAsync();
        errors = process.StandardError.ReadToEndAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataAccess(connectionString);
        services.AddBusinessLogic();
        Services = services.BuildServiceProvider(validateScopes: true);
    }

    public HttpClient CreateClient() => new() { BaseAddress = baseAddress };

    public static async Task<CatalogHost> StartAsync(string databasePath)
    {
        var solutionDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        while (solutionDirectory is not null && !File.Exists(Path.Combine(solutionDirectory.FullName, "ProductCatalog.sln")))
            solutionDirectory = solutionDirectory.Parent;
        if (solutionDirectory is null) throw new InvalidOperationException("Cannot find ProductCatalog.sln.");
        var apiDirectory = Path.Combine(solutionDirectory.FullName, "src", "ProductCatalog.Api");
#if DEBUG
        const string configuration = "Debug";
#else
        const string configuration = "Release";
#endif
        var apiAssembly = Path.Combine(apiDirectory, "bin", configuration, "net10.0", "ProductCatalog.Api.dll");
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var address = new Uri($"http://127.0.0.1:{port}");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = apiDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add(apiAssembly);
        start.ArgumentList.Add("--urls");
        start.ArgumentList.Add(address.ToString());
        start.ArgumentList.Add($"--ConnectionStrings:Catalog={connectionString}");
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        start.Environment["DOTNET_ENVIRONMENT"] = "Development";
        var process = Process.Start(start) ?? throw new InvalidOperationException("Unable to start the API.");
        var host = new CatalogHost(process, address, connectionString);
        try
        {
            using var client = host.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(2);
            var deadline = Stopwatch.StartNew();
            while (deadline.Elapsed < TimeSpan.FromSeconds(30))
            {
                if (process.HasExited)
                    throw new InvalidOperationException($"API exited: {await host.output}\n{await host.errors}");
                try
                {
                    using var response = await client.GetAsync("/api/products");
                    if (response.IsSuccessStatusCode) return host;
                }
                catch (HttpRequestException) { }
                catch (TaskCanceledException) { }
                await Task.Delay(200);
            }
            throw new TimeoutException("API did not become ready in 30 seconds.");
        }
        catch
        {
            await host.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync();
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }
        await Task.WhenAll(output, errors);
        if (!Completed)
        {
            var log = await output + "\n" + await errors;
            Console.Error.WriteLine(log.Length > 12000 ? log[^12000..] : log);
        }
        process.Dispose();
    }
}
