using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Viora.Application.ProfessionalVerifications;
using Viora.Domain.Entities;

namespace viora_BE.Controllers;

[ApiController]
[Authorize]
[Route("api/professional-verifications")]
[ProducesResponseType<ProblemDetails>(400)]
[ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409)]
public sealed class ProfessionalVerificationsController(IProfessionalVerificationService service,
    ILogger<ProfessionalVerificationsController> logger) : ControllerBase
{
    [HttpPost("lawyer")]
    [ProducesResponseType<VerificationResponse>(200)]
    public Task<IActionResult> Start(CancellationToken ct) => Handle(account => service.StartAsync(account, ct));

    [HttpGet("lawyer/me")]
    [ProducesResponseType<VerificationResponse>(200)]
    public Task<IActionResult> Mine(CancellationToken ct) => Handle(account => service.GetMineAsync(account, ct));

    [HttpPut("{id:guid}")]
    [ProducesResponseType<VerificationResponse>(200)]
    public Task<IActionResult> Save(Guid id, SaveVerificationRequest request, CancellationToken ct) =>
        Handle(account => service.SaveAsync(account, id, request, ct));

    [HttpPost("{id:guid}/documents")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 11 * 1024 * 1024)]
    [ProducesResponseType<VerificationResponse>(200)]
    public Task<IActionResult> Upload(Guid id, [FromForm] VerificationUploadRequest request, CancellationToken ct) =>
        Handle(async account =>
        {
            if (request.File is null || request.File.Length is <= 0 or > LawyerVerificationValidation.MaxFileBytes)
                throw new VerificationException(400, "INVALID_FILE_SIZE", "Tài liệu phải có dữ liệu và không vượt quá 10 MB.");
            // Buffer a bounded file to validate magic bytes, then rewind for the provider.
            using var bytes = new MemoryStream();
            await request.File.CopyToAsync(bytes, ct);
            bytes.Position = 0;
            return await service.UploadAsync(account, id, request.DocumentType,
                new(bytes, request.File.FileName, request.File.ContentType, request.File.Length), ct);
        });

    [HttpDelete("{id:guid}/documents/{documentId:guid}")]
    [ProducesResponseType<VerificationResponse>(200)]
    public Task<IActionResult> Delete(Guid id, Guid documentId, CancellationToken ct) =>
        Handle(account => service.DeleteAsync(account, id, documentId, ct));

    [HttpGet("{id:guid}/documents/{documentId:guid}/content")]
    [ProducesResponseType<FileContentResult>(200)]
    public Task<IActionResult> Content(Guid id, Guid documentId, CancellationToken ct) =>
        Handle(async account =>
        {
            var file = await service.ReadAsync(account, id, documentId, ct);
            Response.Headers.CacheControl = "private, no-store";
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            Response.Headers["Content-Security-Policy"] = "sandbox; default-src 'none'";
            // Attachment avoids rendering untrusted PDF content under the API origin.
            return File(file.Content, file.ContentType, file.FileName);
        });

    [HttpPost("{id:guid}/submit")]
    [ProducesResponseType<VerificationResponse>(200)]
    public Task<IActionResult> Submit(Guid id, SubmitVerificationRequest request, CancellationToken ct) =>
        Handle(account => service.SubmitAsync(account, id, request, ct));

    private async Task<IActionResult> Handle<T>(Func<Guid, Task<T>> operation)
    {
        Response.Headers.CacheControl = "private, no-store";
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var accountId)) return Unauthorized();
        try
        {
            var result = await operation(accountId);
            return result is IActionResult action ? action : Ok(result);
        }
        catch (VerificationException e)
        {
            var problem = new ProblemDetails { Status = e.Status, Title = "Verification request failed", Detail = e.Message };
            problem.Extensions["code"] = e.Code;
            if (e.Errors is not null) problem.Extensions["errors"] = e.Errors;
            return new ObjectResult(problem) { StatusCode = e.Status };
        }
        catch (Exception) when (!HttpContext.RequestAborted.IsCancellationRequested)
        {
            // Do not log exceptions that may contain private file URLs, credentials or form values.
            logger.LogWarning("Professional verification operation failed.");
            return Problem(statusCode: 503, detail: "Không thể xử lý yêu cầu lúc này. Vui lòng thử lại.");
        }
    }
}

public sealed class VerificationUploadRequest
{
    public VerificationDocumentType DocumentType { get; set; }
    public IFormFile File { get; set; } = null!;
}
