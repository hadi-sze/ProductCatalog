using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ProductCatalog.BLL.Exceptions;
using ProductCatalog.DAL.Exceptions;

namespace ProductCatalog.Api.ErrorHandling;

public sealed class ApiExceptionHandler(IProblemDetailsService problemDetailsService,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var status = exception switch
        {
            BusinessValidationException => StatusCodes.Status400BadRequest,
            ProductNotFoundException or MainCompanyNotFoundException => StatusCodes.Status404NotFound,
            ProductConflictException or DuplicateSkuException or DuplicateCompanyCodeException or PersistenceConflictException => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };
        if (status == 500) logger.LogError(exception, "Unhandled error for {Method} {Path}", context.Request.Method, context.Request.Path);
        else logger.LogInformation("Request failed with {Status}: {Message}", status, exception.Message);

        ProblemDetails problem = exception is BusinessValidationException validation
            ? new ValidationProblemDetails(validation.Errors)
            : new ProblemDetails();
        problem.Status = status;
        problem.Title = status switch
        {
            400 => "Validation failed",
            404 => "Resource not found",
            409 => "Resource conflict",
            _ => "An unexpected error occurred"
        };
        problem.Detail = status == 500 ? "The request could not be completed. Try again later." : exception.Message;
        problem.Instance = context.Request.Path;
        problem.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;
        context.Response.StatusCode = status;
        await problemDetailsService.WriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem });
        return true;
    }
}
