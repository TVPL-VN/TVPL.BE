using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Viora.Application.Statutory;

namespace viora_BE.Controllers;

public sealed class StatutoryErrorsAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        var error = context.Exception as StatutoryException;
        if (error is null && context.Exception is DbUpdateException { InnerException: PostgresException { SqlState: "23505" or "23503" } })
            error = new(409, "Dữ liệu đã thay đổi hoặc đang được tham chiếu. Vui lòng tải lại.");
        if (error is null) return;
        context.Result = new ObjectResult(new ProblemDetails { Status = error.Status, Title = "Không thể xử lý văn bản pháp luật", Detail = error.Message }) { StatusCode = error.Status };
        context.ExceptionHandled = true;
    }
}
[ApiController, Route("api/legal-documents"), StatutoryErrors]
public sealed class StatutoryController(IStatutoryRepository repository) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] StatutoryQuery query, CancellationToken t) => Ok(await repository.SearchAsync(query, false, t));
    [HttpGet("catalog")]
    public async Task<IActionResult> Catalog(CancellationToken t) => Ok(await repository.CatalogAsync(t));
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, [FromQuery] DateOnly? at, CancellationToken t)
    {
        var result = await repository.DetailAsync(id, false, at, t); return result is null ? NotFound() : Ok(result);
    }
    [HttpGet("{id:guid}/sections")]
    public async Task<IActionResult> Sections(Guid id, [FromQuery] DateOnly? at, CancellationToken t)
    {
        var result = await repository.DetailAsync(id, false, at, t); return result is null ? NotFound() : Ok(result.Sections);
    }
    [HttpGet("{id:guid}/relations")]
    public async Task<IActionResult> Relations(Guid id, CancellationToken t)
    {
        var result = await repository.DetailAsync(id, false, null, t); return result is null ? NotFound() : Ok(result.Relations);
    }
    [HttpGet("sections/{id:guid}/versions")]
    public async Task<IActionResult> Versions(Guid id, CancellationToken t) => Ok(await repository.VersionsAsync(id, false, t));
}
