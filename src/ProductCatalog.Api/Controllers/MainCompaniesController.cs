using Microsoft.AspNetCore.Mvc;
using ProductCatalog.BLL.DTOs;
using ProductCatalog.BLL.Services;

namespace ProductCatalog.Api.Controllers;

[ApiController]
[Route("api/main-companies")]
public sealed class MainCompaniesController(IMainCompanyService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<MainCompanyResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<MainCompanyResponse>>> GetAll(
        CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        Ok(await service.GetPageAsync(page, pageSize, cancellationToken));

    [HttpGet("{id:int:min(1)}")]
    [ProducesResponseType<MainCompanyResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MainCompanyResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var company = await service.GetByIdAsync(id, cancellationToken);
        return company is null ? NotFound() : Ok(company);
    }

    [HttpPost]
    [ProducesResponseType<MainCompanyResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MainCompanyResponse>> Create(MainCompanyWriteRequest request, CancellationToken cancellationToken)
    {
        var company = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = company.Id }, company);
    }

    [HttpPut("{id:int:min(1)}")]
    [ProducesResponseType<MainCompanyResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MainCompanyResponse>> Update(int id, MainCompanyWriteRequest request, CancellationToken cancellationToken) =>
        Ok(await service.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:int:min(1)}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
