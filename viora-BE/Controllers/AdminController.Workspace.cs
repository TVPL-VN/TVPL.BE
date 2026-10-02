using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Viora.Application.Admin;
using Viora.Application.ProfessionalVerifications;
using Viora.Domain.Entities;

namespace viora_BE.Controllers;

public sealed partial class AdminController
{
    [HttpGet("dashboard/work")]
    public Task<IActionResult> Work(CancellationToken ct) => AdminRead(() => workspace.OverviewAsync(ct));

    [HttpGet("professional-verifications")]
    public Task<IActionResult> ProfessionalVerifications([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] VerificationStatus status = VerificationStatus.Pending, [FromQuery] string? keyword = null, CancellationToken ct = default) =>
        AdminRead(() => workspace.VerificationsAsync(page, pageSize, status, keyword, ct));

    [HttpGet("professional-verifications/{id:guid}")]
    public Task<IActionResult> ProfessionalVerification(Guid id, CancellationToken ct) => AdminRead(() => workspace.VerificationAsync(id, ct));

    [HttpPost("professional-verifications/{id:guid}/approve")]
    public Task<IActionResult> ApproveProfessionalVerification(Guid id, AdminReviewRequest request, CancellationToken ct) =>
        AdminRead(async () => await workspace.ReviewAsync(await AdminId(ct), id, true, request, ct));

    [HttpPost("professional-verifications/{id:guid}/reject")]
    public Task<IActionResult> RejectProfessionalVerification(Guid id, AdminReviewRequest request, CancellationToken ct) =>
        AdminRead(async () => await workspace.ReviewAsync(await AdminId(ct), id, false, request, ct));

    [HttpGet("professional-verifications/{id:guid}/documents/{documentId:guid}/content")]
    public async Task<IActionResult> ProfessionalDocument(Guid id, Guid documentId, CancellationToken ct)
    {
        try
        {
            var file = await workspace.DocumentAsync(await AdminId(ct), id, documentId, ct);
            Response.Headers.CacheControl = "private, no-store";
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            Response.Headers["Content-Security-Policy"] = "sandbox; default-src 'none'";
            return File(file.Content, file.ContentType, file.FileName);
        }
        catch (VerificationException e) { return AdminFailure(e); }
    }

    [HttpGet("users/{id:guid}/context")]
    public Task<IActionResult> UserContext(Guid id, CancellationToken ct) => AdminRead(() => workspace.UserContextAsync(id, ct));

    [HttpPost("users/{id:guid}/revoke-sessions")]
    public Task<IActionResult> RevokeSessions(Guid id, AdminUserDecisionRequest request, CancellationToken ct) =>
        AdminMutation(async () => await workspace.UserActionAsync(await AdminId(ct), id, "RevokeSessions", request, null, null, ct));

    [HttpGet("logs/{id:guid}")]
    public Task<IActionResult> AuditDetail(Guid id, CancellationToken ct) => AdminRead(() => workspace.AuditAsync(id, ct));

    private async Task<Guid> AdminId(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var account)) throw new VerificationException(401, "UNAUTHENTICATED", "Bạn chưa đăng nhập.");
        return await workspace.ActiveAdminAsync(account, ct) ?? throw new VerificationException(403, "ADMIN_REQUIRED", "Bạn không có quyền quản trị.");
    }
    private async Task<IActionResult> AdminRead<T>(Func<Task<T>> call)
    {
        Response.Headers.CacheControl = "private, no-store";
        try { return OkResponse(await call()); }
        catch (VerificationException e) { return AdminFailure(e); }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException) { return ConflictResponse("Dữ liệu vừa được quản trị viên khác xử lý. Hãy tải lại."); }
    }
    private Task<IActionResult> AdminMutation(Func<Task> call) => AdminRead(async () => { await call(); return new AdminMutationResponse(true, "Thao tác đã được ghi nhận."); });
    private static IActionResult AdminFailure(VerificationException e) => new ObjectResult(new { success = false, message = e.Message, code = e.Code, errors = e.Errors }) { StatusCode = e.Status };
}
