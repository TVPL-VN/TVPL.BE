using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Viora.Application.Statutory;

namespace viora_BE.Controllers;

[ApiController, Route("api/admin/legal-documents"), Authorize(Policy = "ActiveAdmin"), StatutoryErrors]
public sealed class AdminStatutoryController(IStatutoryRepository repository) : ControllerBase
{
    private Guid Actor => Guid.TryParse(User.FindFirstValue("user_id"), out var id) ? id : throw new StatutoryException(401, "Phiên đăng nhập không hợp lệ.");
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] StatutoryQuery query, CancellationToken t) => Ok(await repository.SearchAsync(query, true, t));
    [HttpGet("catalog")]
    public async Task<IActionResult> Catalog(CancellationToken t) => Ok(await repository.CatalogAsync(t));
    [HttpPost("catalog/{kind}")]
    public async Task<IActionResult> AddCatalog(string kind, CatalogRequest r, CancellationToken t)
    {
        if (kind != "authorities" && kind != "fields") return BadRequest();
        return Ok(await repository.AddCatalogAsync(kind == "authorities", r.Name, t));
    }
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, [FromQuery] DateOnly? at, CancellationToken t)
    {
        var result = await repository.DetailAsync(id, true, at, t); return result is null ? NotFound() : Ok(result);
    }
    [HttpPost]
    public async Task<IActionResult> Create(SaveStatutoryRequest r, CancellationToken t)
    {
        var d = await repository.SaveAsync(null, Actor, r, t); return CreatedAtAction(nameof(Detail), new { id = d.Id }, d);
    }
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, SaveStatutoryRequest r, CancellationToken t) => Ok(await repository.SaveAsync(id, Actor, r, t));
    [HttpPost("{id:guid}/publication")]
    public async Task<IActionResult> Publication(Guid id, PublicationRequest r, CancellationToken t) { await repository.PublishAsync(id, Actor, r.Publish, t); return NoContent(); }
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken t) { await repository.DeleteAsync(id, t); return NoContent(); }
    [HttpPost("{docId:guid}/sections")]
    public async Task<IActionResult> CreateSection(Guid docId, SaveSectionRequest r, CancellationToken t) => Ok(await repository.SaveSectionAsync(docId, null, Actor, r, t));
    [HttpPut("{docId:guid}/sections/{id:guid}")]
    public async Task<IActionResult> UpdateSection(Guid docId, Guid id, SaveSectionRequest r, CancellationToken t) => Ok(await repository.SaveSectionAsync(docId, id, Actor, r, t));
    [HttpDelete("sections/{id:guid}")]
    public async Task<IActionResult> DeleteSection(Guid id, CancellationToken t) { await repository.DeleteSectionAsync(id, t); return NoContent(); }
    [HttpGet("sections/{id:guid}/versions")]
    public async Task<IActionResult> Versions(Guid id, CancellationToken t) => Ok(await repository.VersionsAsync(id, true, t));
    [HttpPost("sections/{id:guid}/versions")]
    public async Task<IActionResult> Amend(Guid id, AmendSectionRequest r, CancellationToken t) => Ok(await repository.AmendAsync(id, Actor, r, t));
    [HttpPost("sections/{id:guid}/versions/{versionId:guid}/publish")]
    public async Task<IActionResult> PublishVersion(Guid id, Guid versionId, CancellationToken t) { await repository.PublishVersionAsync(id, versionId, Actor, t); return NoContent(); }
    [HttpPost("{id:guid}/relations")]
    public async Task<IActionResult> AddRelation(Guid id, SaveRelationRequest r, CancellationToken t) { await repository.AddRelationAsync(id, r, t); return NoContent(); }
    [HttpDelete("{id:guid}/relations/{relationId:guid}")]
    public async Task<IActionResult> DeleteRelation(Guid id, Guid relationId, CancellationToken t) { await repository.DeleteRelationAsync(id, relationId, t); return NoContent(); }
    [HttpPost("sections/{id:guid}/relations")]
    public async Task<IActionResult> AddSectionRelation(Guid id, SaveSectionRelationRequest r, CancellationToken t) { await repository.AddSectionRelationAsync(id, r, t); return NoContent(); }
}
public sealed record CatalogRequest([property: Required, MaxLength(255)] string Name);
public sealed record PublicationRequest(bool Publish);
